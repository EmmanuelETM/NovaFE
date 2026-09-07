using System.Globalization;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Webhooks.Interfaces;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Webhooks.Delivery;

/// <summary>
/// Procesa una fila de <c>webhook_deliveries</c>: la entrega y aplica el
/// resultado. La llamada HTTP ocurre fuera de transacción; el resultado (estado
/// de la fila + contador de fallos del endpoint) se confirma en una.
/// </summary>
public sealed class WebhookDeliveryProcessor(
    IWebhookOutbox outbox,
    IWebhookEndpointRepository endpoints,
    IWebhookSender sender,
    WebhookSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<WebhookDeliveryProcessor> logger)
{
    public async Task ProcessAsync(WebhookDeliveryItem item, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var endpoint = await endpoints.GetAsync(item.EndpointId, ct);
        if (endpoint is null || !endpoint.Enabled)
        {
            // El endpoint se borró o quedó deshabilitado: la entrega se descarta.
            await outbox.CompleteAsync(item.RowId, statusCode: 0, ct);
            return;
        }

        var attempt = await sender.SendAsync(
            new WebhookDeliveryRequest(
                endpoint.Url, endpoint.Secret, item.EventType, item.RowId.ToString("N"), item.Payload),
            ct);

        if (attempt.Success)
        {
            await unitOfWork.ExecuteInTransactionAsync(async t =>
            {
                await outbox.CompleteAsync(item.RowId, attempt.StatusCode ?? 200, t);
                endpoint.RecordDeliverySuccess();
                await endpoints.UpdateAsync(endpoint, t);
            }, ct);
            return;
        }

        var nextAttempts = item.Attempts + 1;
        var isDead = nextAttempts >= settings.MaxAttempts;

        await unitOfWork.ExecuteInTransactionAsync(async t =>
        {
            endpoint.RecordDeliveryFailure(settings.AutoDisableAfterConsecutiveFailures, timeProvider.GetUtcNow());
            await endpoints.UpdateAsync(endpoint, t);

            if (isDead)
                await outbox.MarkDeadAsync(item.RowId, attempt.StatusCode, attempt.Error, t);
            else
                await outbox.RescheduleAsync(
                    item.RowId,
                    timeProvider.GetUtcNow() + settings.BackoffFor(item.Attempts),
                    nextAttempts,
                    attempt.StatusCode,
                    attempt.Error,
                    t);
        }, ct);

        if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Entrega {EventType} al webhook {EndpointId} falló (intento {Attempt}/{Max}{Dead}): {Reason}",
                item.EventType, item.EndpointId, nextAttempts, settings.MaxAttempts,
                isDead ? ", agotado" : string.Empty,
                attempt.Error ?? attempt.StatusCode?.ToString(CultureInfo.InvariantCulture));
        }
    }
}
