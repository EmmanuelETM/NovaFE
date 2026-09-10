using Microsoft.Extensions.Options;
using NovaFE.Application.Webhooks.Delivery;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="IWebhookDeliveryPump.RunOnceAsync"/> en intervalo. Cada tick
/// es independiente; un fallo se registra y no detiene el worker. Multi-instancia
/// seguro (el reclamo usa <c>FOR UPDATE SKIP LOCKED</c>). Con la cola vacía la
/// espera crece hasta <c>Webhooks:MaxPollIntervalSeconds</c>.
/// </summary>
internal sealed class WebhookDeliveryWorker(
    IWebhookDeliveryPump pump,
    IOptionsMonitor<WebhooksOptions> options,
    ILogger<WebhookDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseInterval = options.CurrentValue.PollInterval;
        logger.LogInformation(
            "Worker de entrega de webhooks iniciado (intervalo {Interval}, hasta {Max} sin trabajo)",
            baseInterval, options.CurrentValue.MaxPollInterval);

        await SafeDelayAsync(Jitter(baseInterval), stoppingToken);

        var delay = baseInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Se releen en cada iteración: un cambio en caliente ajusta el ritmo
            // sin reiniciar el worker.
            baseInterval = options.CurrentValue.PollInterval;
            var maxInterval = options.CurrentValue.MaxPollInterval;

            var processed = 0;

            try
            {
                processed = await pump.RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "El tick del worker de entrega de webhooks falló");
            }

            delay = processed > 0
                ? baseInterval
                : TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, maxInterval.Ticks));

            await SafeDelayAsync(delay + Jitter(delay), stoppingToken);
        }
    }

    private static TimeSpan Jitter(TimeSpan interval)
        => TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(interval.TotalMilliseconds / 2)));

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
