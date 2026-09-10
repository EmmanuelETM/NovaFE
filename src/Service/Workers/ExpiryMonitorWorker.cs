using NovaFE.Application.Notifications;
using NovaFE.Service.Configuration;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="IExpiryMonitorPump.RunOnceAsync"/> cada
/// <c>ExpiryMonitor:IntervalHours</c>. Un fallo se registra y no detiene el
/// worker; el primer barrido corre poco después del arranque.
/// </summary>
internal sealed class ExpiryMonitorWorker(
    IExpiryMonitorPump pump,
    IOptionsMonitor<ExpiryMonitorOptions> options,
    ILogger<ExpiryMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Monitor de vencimientos iniciado (intervalo {Interval})", options.CurrentValue.Interval);

        await SafeDelayAsync(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var scanned = await pump.RunOnceAsync(stoppingToken);
                logger.LogDebug("Barrido de vencimientos: {Count} tenant(s)", scanned);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "El barrido del monitor de vencimientos falló");
            }

            // Se relee en cada iteración: un cambio en caliente aplica al próximo ciclo.
            await SafeDelayAsync(options.CurrentValue.Interval, stoppingToken);
        }
    }

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
