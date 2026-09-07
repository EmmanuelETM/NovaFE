using NovaFE.Application.Webhooks.Delivery;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="IWebhookDeliveryPump.RunOnceAsync"/> en intervalo. Cada tick
/// es independiente; un fallo se registra y no detiene el worker. Multi-instancia
/// seguro (el reclamo usa <c>FOR UPDATE SKIP LOCKED</c>).
/// </summary>
internal sealed class WebhookDeliveryWorker(
    IWebhookDeliveryPump pump,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de entrega de webhooks iniciado (intervalo {Interval})", Interval);

        await SafeDelayAsync(Jitter(), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await pump.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "El tick del worker de entrega de webhooks falló");
            }

            await SafeDelayAsync(Interval + Jitter(), stoppingToken);
        }
    }

    private static TimeSpan Jitter()
        => TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(Interval.TotalMilliseconds / 2)));

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Apagado en curso.
        }
    }
}
