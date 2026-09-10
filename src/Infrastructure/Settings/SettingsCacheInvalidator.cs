using NovaFE.Application.Settings.Interfaces;
using Microsoft.Extensions.Logging;

namespace NovaFE.Infrastructure.Settings;

/// <summary>
/// Recarga incondicional del snapshot local: lo llama el caso de uso tras escribir
/// (para verlo al instante en esta instancia) y el pump cuando detecta una
/// generación nueva.
/// </summary>
internal sealed class SettingsCacheInvalidator(
    ISettingsSnapshotLoader loader,
    ISettingsSnapshotHolder holder,
    ILogger<SettingsCacheInvalidator> logger) : ISettingsCacheInvalidator
{
    public async Task RefreshNowAsync(CancellationToken ct = default)
    {
        var snapshot = await loader.LoadAsync(ct);
        holder.Replace(snapshot);

        logger.LogDebug(
            "Snapshot de settings recargado: generación {Generation}, {Count} override(s)",
            snapshot.Generation, snapshot.Overrides.Count);
    }
}
