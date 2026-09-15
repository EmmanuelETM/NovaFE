using Microsoft.Extensions.Options;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Maintenance;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="IRetentionPump.RunOnceAsync"/> cada
/// <c>Retention:IntervalHours</c>. Un fallo se registra y no detiene el worker.
/// </summary>
internal sealed class RetentionWorker(
    IRetentionPump pump,
    IOptionsMonitor<RetentionOptions> options,
    IWorkerHeartbeat heartbeat,
    ILogger<RetentionWorker> logger) : BackgroundService
{
    private const string HeartbeatName = "retention";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Worker de retención iniciado (intervalo {Interval})", options.CurrentValue.Interval);

        await SafeDelayAsync(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var purged = await pump.RunOnceAsync(stoppingToken);
                logger.LogDebug("Barrido de retención: {Count} fila(s) purgada(s)", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "El barrido de retención falló");
            }

            heartbeat.Beat(HeartbeatName, options.CurrentValue.Interval * 3);

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
