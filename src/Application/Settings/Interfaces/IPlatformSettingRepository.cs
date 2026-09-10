using NovaFE.Application.Settings.Contracts;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Write side (EF Core) de los overrides de settings de plataforma. La bitácora de
/// cambios y el contador de generación son <see cref="IPlatformSettingChangeLog"/>
/// e <see cref="ISettingsGenerationStore"/>: el caso de uso los orquesta en una
/// sola transacción.
/// </summary>
public interface IPlatformSettingRepository
{
    /// <summary>El override de una clave (ambiente agnóstico), o null si rige el default.</summary>
    Task<PlatformSettingRecord?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Crea o reemplaza el override de una clave.</summary>
    Task UpsertAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Quita el override (vuelta al default). Devuelve <c>false</c> si no había.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);
}
