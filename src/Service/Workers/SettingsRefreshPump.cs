using NovaFE.Application.Settings.Interfaces;

namespace NovaFE.Service.Workers;

/// <summary>
/// Un tick del refresco de settings: lee el contador de generación en su propio
/// scope y, si difiere del último visto, recarga el snapshot. La primera llamada
/// siempre recarga (el <c>_known</c> arranca en <see cref="long.MinValue"/>).
/// Ver <c>docs/configuration.md</c>.
/// </summary>
internal sealed class SettingsRefreshPump(
    IServiceScopeFactory scopeFactory,
    ILogger<SettingsRefreshPump> logger) : ISettingsRefreshPump
{
    private long _known = long.MinValue;

    public async Task<bool> RunOnceAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var generation = await scope.ServiceProvider
            .GetRequiredService<ISettingsGenerationStore>()
            .CurrentAsync(ct);

        if (generation == _known)
            return false;

        await scope.ServiceProvider
            .GetRequiredService<ISettingsCacheInvalidator>()
            .RefreshNowAsync(ct);

        var previous = _known;
        _known = generation;

        if (previous != long.MinValue)
            logger.LogInformation(
                "Settings: snapshot recargado por cambio de generación {Previous} → {Generation}",
                previous, generation);

        return true;
    }
}
