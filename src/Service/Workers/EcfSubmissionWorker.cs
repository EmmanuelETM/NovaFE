using Microsoft.Extensions.Options;
using NovaFE.Application.Ecf.Submission;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="IEcfSubmissionPump.RunOnceAsync"/> en intervalo. Cada tick es
/// independiente; un fallo se registra y no detiene el worker. Multi-instancia
/// seguro (el reclamo usa <c>FOR UPDATE SKIP LOCKED</c>).
/// </summary>
internal sealed class EcfSubmissionWorker(
    IEcfSubmissionPump pump,
    IOptionsMonitor<EcfSubmissionOptions> options,
    ILogger<EcfSubmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseInterval = options.CurrentValue.PollInterval;
        logger.LogInformation(
            "Worker de envío a la DGII iniciado (intervalo {Interval}, hasta {Max} sin trabajo)",
            baseInterval, options.CurrentValue.MaxPollInterval);

        // Arranque escalonado entre instancias.
        await SafeDelayAsync(Jitter(baseInterval), stoppingToken);

        var delay = baseInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            // Se releen en cada iteración: un cambio de configuración en caliente
            // ajusta el ritmo sin reiniciar el worker.
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
                logger.LogError(ex, "El tick del worker de envío falló");
            }

            // Con trabajo, ritmo base; con la cola vacía, se duplica la espera
            // hasta el techo — así una instancia ociosa deja de martillar la base.
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
