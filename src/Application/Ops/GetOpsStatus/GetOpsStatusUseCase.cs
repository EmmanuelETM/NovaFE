using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ops.Contracts;
using NovaFE.Application.Ops.Interfaces;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Domain.Settings;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ops.GetOpsStatus;

/// <summary>
/// Estado operacional en vivo: latido de los workers de fondo, profundidad y
/// antigüedad de los outbox (DGII + webhooks), y qué tenants tienen una
/// secuencia e-NCF por agotarse. Ver docs/observability.md.
/// </summary>
public sealed class GetOpsStatusUseCase(
    ILoggerFactory loggerFactory,
    IWorkerHeartbeat heartbeat,
    IOpsStatusReadRepository readRepository,
    ISettingsReader settingsReader,
    TimeProvider timeProvider)
    : ParameterlessQueryUseCase<OpsStatusDto>(loggerFactory)
{
    protected override async Task<ErrorOr<OpsStatusDto>> ExecuteCore(NoRequest request, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();

        var workers = heartbeat.Entries
            .Select(entry => new WorkerStatusDto(
                Name: entry.Key,
                LastBeatAt: entry.Value.LastBeatAt,
                MaxSilence: entry.Value.MaxSilence,
                Healthy: now - entry.Value.LastBeatAt <= entry.Value.MaxSilence))
            .OrderBy(w => w.Name, StringComparer.Ordinal)
            .ToArray();

        var lowStockFraction = settingsReader.GetValue(SettingDefinitions.SequenceLowStockFraction);

        var ecfOutbox = await readRepository.GetEcfSubmissionOutboxStatusAsync(ct);
        var webhookOutbox = await readRepository.GetWebhookOutboxStatusAsync(ct);
        var sequencesAtRisk = await readRepository.GetSequencesAtRiskAsync(lowStockFraction, ct);

        return new OpsStatusDto(now, workers, ecfOutbox, webhookOutbox, sequencesAtRisk);
    }
}
