using NovaFE.Application.Certificates.Interfaces;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Application.Webhooks;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Settings;
using NovaFE.Domain.Webhooks;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Notifications;

/// <summary>
/// Revisa los certificados y las secuencias del tenant en curso y emite un
/// webhook cuando alguno cruza un umbral de vencimiento o de stock (RF-01.6). El
/// <see cref="IExpiryNotificationLog"/> hace que cada aviso salga una sola vez.
/// El tenant lo fija la capa Service antes de resolver este servicio.
/// </summary>
public sealed class ExpiryScan(
    ICertificateReadRepository certificates,
    INcfSequenceReadRepository sequences,
    IWebhookOutbox webhookOutbox,
    IExpiryNotificationLog log,
    IUnitOfWork unitOfWork,
    ISettingsReader settingsReader,
    TimeProvider timeProvider,
    ILogger<ExpiryScan> logger)
{
    // RF-01.6: alertas escalonadas antes del vencimiento — defaults de respaldo
    // si el setting runtime queda corrupto (nunca debería, se valida al escribir).
    private static readonly int[] CertificateThresholdsDaysFallback = [90, 30, 15, 7];
    private static readonly int[] SequenceThresholdsDaysFallback = [30, 7];

    public async Task RunForCurrentTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var today = timeProvider.GetDominicanToday();

        await ScanCertificatesAsync(tenantId, now, ct);
        await ScanSequencesAsync(tenantId, now, today, ct);
    }

    private async Task ScanCertificatesAsync(Guid tenantId, DateTimeOffset now, CancellationToken ct)
    {
        var thresholds = ParseThresholds(
            settingsReader.GetValue(SettingDefinitions.CertificateExpiryThresholdsDays), CertificateThresholdsDaysFallback);

        foreach (var cert in await certificates.ListAsync(ct))
        {
            if (!string.Equals(cert.Status, "Active", StringComparison.Ordinal))
                continue;

            if (cert.ValidTo <= now)
            {
                await NotifyAsync("certificate", cert.Id, ["expired"],
                    WebhookEventType.CertificateExpired, cert, tenantId, now, ct);
                continue;
            }

            var daysLeft = (int)Math.Floor((cert.ValidTo - now).TotalDays);
            var crossed = thresholds.Where(t => daysLeft <= t).Select(t => $"expiring:{t}").ToArray();

            if (crossed.Length > 0)
                await NotifyAsync("certificate", cert.Id, crossed,
                    WebhookEventType.CertificateExpiring, cert, tenantId, now, ct);
        }
    }

    private async Task ScanSequencesAsync(Guid tenantId, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        var thresholds = ParseThresholds(
            settingsReader.GetValue(SettingDefinitions.SequenceExpiryThresholdsDays), SequenceThresholdsDaysFallback);

        foreach (var seq in await sequences.ListAsync(ct))
        {
            if (!seq.Active)
                continue;

            if (seq.Remaining == 0)
            {
                await NotifyAsync("sequence", seq.Id, ["exhausted"],
                    WebhookEventType.SequenceExhausted, seq, tenantId, now, ct);
            }
            else if (seq.IsLowStock)
            {
                await NotifyAsync("sequence", seq.Id, ["low"],
                    WebhookEventType.SequenceLow, seq, tenantId, now, ct);
            }

            if (seq.ExpiresOn is not { } expiresOn)
                continue;

            if (today > expiresOn)
            {
                await NotifyAsync("sequence", seq.Id, ["expired"],
                    WebhookEventType.SequenceExpired, seq, tenantId, now, ct);
                continue;
            }

            var daysLeft = expiresOn.DayNumber - today.DayNumber;
            var crossed = thresholds.Where(t => daysLeft <= t).Select(t => $"expiring:{t}").ToArray();

            if (crossed.Length > 0)
                await NotifyAsync("sequence", seq.Id, crossed,
                    WebhookEventType.SequenceExpiring, seq, tenantId, now, ct);
        }
    }

    /// <summary>
    /// Interpreta un setting tipo "lista de días" (p. ej. <c>"90,30,15,7"</c>). Un
    /// token que no parsea cae al <paramref name="fallback"/> completo — igual
    /// criterio que <c>CachedSettingsReader</c>: un override corrupto nunca lanza.
    /// </summary>
    private static IReadOnlyList<int> ParseThresholds(string raw, IReadOnlyList<int> fallback)
    {
        var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return fallback;

        var result = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var value) || value <= 0)
                return fallback;

            result.Add(value);
        }

        return result;
    }

    /// <summary>
    /// Registra los <paramref name="kinds"/> y, si alguno es nuevo, encola el
    /// webhook — todo en una transacción, para que un fallo al encolar no deje el
    /// aviso marcado como enviado.
    /// </summary>
    private Task NotifyAsync(
        string subjectType, Guid subjectId, IReadOnlyList<string> kinds,
        string eventType, object payload, Guid tenantId, DateTimeOffset now, CancellationToken ct)
        => unitOfWork.ExecuteInTransactionAsync(async t =>
        {
            var anyNew = false;
            foreach (var kind in kinds)
                anyNew |= await log.TryRecordAsync(subjectType, subjectId, kind, t);

            if (!anyNew)
                return;

            await webhookOutbox.EnqueueAsync(WebhookEvent.Create(eventType, payload, now), tenantId, t);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("Aviso {EventType} para {SubjectType} {SubjectId}", eventType, subjectType, subjectId);
        }, ct);
}
