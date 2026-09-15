using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Contingency;
using NovaFE.Service.Configuration;
using Microsoft.Extensions.Options;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="IContingencyMonitorPump.RunOnceAsync"/> cada
/// <c>ContingencyMonitor:IntervalSeconds</c> (M11 Tipo 1). Un fallo se registra y
/// no detiene el worker; el primer barrido corre poco después del arranque.
/// </summary>
internal sealed class ContingencyMonitorWorker(
    IContingencyMonitorPump pump,
    IOptionsMonitor<ContingencyMonitorOptions> options,
    IWorkerHeartbeat heartbeat,
    ILogger<ContingencyMonitorWorker> logger) : BackgroundService
{
    private const string HeartbeatName = "contingency-monitor";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Monitor de contingencia iniciado (intervalo {Interval})", options.CurrentValue.Interval);

        await SafeDelayAsync(TimeSpan.FromSeconds(15), stoppingToken);

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
                logger.LogError(ex, "El barrido del monitor de contingencia falló");
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
