using Microsoft.Extensions.Options;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Service.Common;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un tick de la entrega de webhooks: recupera filas atascadas, reclama un lote
/// (la tabla de deliveries no lleva RLS) y procesa cada fila en su propio scope
/// con el tenant de la fila fijado en <see cref="CurrentTenant"/> — así el lookup
/// del endpoint queda acotado al tenant correcto. Multi-instancia seguro
/// (<c>FOR UPDATE SKIP LOCKED</c>).
/// </summary>
internal sealed class WebhookDeliveryPump(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WebhooksOptions> options,
    TimeProvider timeProvider,
    ILogger<WebhookDeliveryPump> logger) : IWebhookDeliveryPump
{
    private const int BatchSize = 50;

    /// <summary>La purga del log viejo es mantenimiento: basta una vez por hora.</summary>
    private static readonly TimeSpan PurgeInterval = TimeSpan.FromHours(1);

    private DateTimeOffset _lastPurge = DateTimeOffset.MinValue;

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        // Se relee por tick: los timeouts y la retención se ajustan en caliente.
        var settings = options.CurrentValue.ToSettings();

        using var claimScope = scopeFactory.CreateScope();
        var outbox = claimScope.ServiceProvider.GetRequiredService<IWebhookOutbox>();

        var reaped = await outbox.ReapStuckAsync(settings.DeliveryStuckAfter, ct);
        if (reaped > 0)
            logger.LogInformation("Recuperadas {Count} entregas de webhook atascadas", reaped);

        var batch = await outbox.ClaimBatchAsync(BatchSize, ct);

        // En un tick sin trabajo se aprovecha para purgar el log viejo — pero a lo
        // sumo una vez por hora, no en cada tick ocioso.
        if (batch.Count == 0)
        {
            var now = timeProvider.GetUtcNow();
            if (now - _lastPurge >= PurgeInterval)
            {
                _lastPurge = now;
                var purged = await outbox.PurgeAsync(settings.DeliveriesRetention, ct);
                if (purged > 0)
                    logger.LogInformation("Purgadas {Count} entregas de webhook del log", purged);
            }

            return 0;
        }

        var processed = 0;
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var itemScope = scopeFactory.CreateScope();
                itemScope.ServiceProvider.GetRequiredService<CurrentTenant>().Set(item.TenantId);

                await itemScope.ServiceProvider
                    .GetRequiredService<WebhookDeliveryProcessor>()
                    .ProcessAsync(item, ct);

                processed++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Falló el procesamiento de la entrega de webhook {RowId} (evento {EventId})", item.RowId, item.EventId);
            }
        }

        return processed;
    }
}
