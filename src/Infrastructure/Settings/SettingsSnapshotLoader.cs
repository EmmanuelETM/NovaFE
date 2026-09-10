using NovaFE.Application.Settings.Interfaces;

namespace NovaFE.Infrastructure.Settings;

/// <summary>Arma un <see cref="SettingsSnapshot"/> leyendo la generación y los overrides.</summary>
internal interface ISettingsSnapshotLoader
{
    Task<SettingsSnapshot> LoadAsync(CancellationToken ct = default);
}

internal sealed class SettingsSnapshotLoader(
    ISettingsGenerationStore generationStore,
    IPlatformSettingReadRepository readRepository) : ISettingsSnapshotLoader
{
    public async Task<SettingsSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var generation = await generationStore.CurrentAsync(ct);
        var rows = await readRepository.ListAsync(ct);

        // Este slice solo resuelve overrides agnósticos de ambiente (environment = "").
        var overrides = rows
            .Where(r => r.Environment.Length == 0)
            .ToDictionary(r => r.Key, r => r.Value, StringComparer.Ordinal);

        return new SettingsSnapshot(generation, overrides);
    }
}
