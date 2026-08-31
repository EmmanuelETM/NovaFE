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
    IOptions<EcfSubmissionOptions> options,
    ILogger<EcfSubmissionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.PollInterval;
        logger.LogInformation("Worker de envío a la DGII iniciado (intervalo {Interval})", interval);

        // Arranque escalonado entre instancias.
        await SafeDelayAsync(Jitter(interval), stoppingToken);

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
                logger.LogError(ex, "El tick del worker de envío falló");
            }

            await SafeDelayAsync(interval + Jitter(interval), stoppingToken);
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
