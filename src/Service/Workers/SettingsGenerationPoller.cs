using Microsoft.Extensions.Options;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Service.Configuration;

namespace NovaFE.Service.Workers;

/// <summary>
/// Dispara <see cref="ISettingsRefreshPump.RunOnceAsync"/> cada
/// <c>Settings:PollIntervalSeconds</c>. Es la propagación de settings sin Redis:
/// una query trivial al contador de generación, multi-réplica-safe. Un fallo se
/// registra y no detiene el poller (se mantiene el snapshot vigente).
/// </summary>
internal sealed class SettingsGenerationPoller(
    ISettingsRefreshPump pump,
    IOptionsMonitor<SettingsOptions> options,
    ILogger<SettingsGenerationPoller> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Poller de settings iniciado (intervalo {Interval})", options.CurrentValue.PollInterval);

        // Primer tick inmediato: el warm-load de Program.cs ya cargó el snapshot
        // antes de aceptar tráfico, pero esto deja al pump con su _known al día.
        await TickAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await SafeDelayAsync(options.CurrentValue.PollInterval, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
                break;

            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            await pump.RunOnceAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Apagado en curso.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "El poll de settings falló; se mantiene el snapshot vigente");
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
