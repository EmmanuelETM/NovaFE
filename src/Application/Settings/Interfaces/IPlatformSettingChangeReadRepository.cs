using NovaFE.Application.Settings.Contracts;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>Lectura de la bitácora de cambios de settings (Dapper).</summary>
public interface IPlatformSettingChangeReadRepository
{
    /// <summary>El historial de una clave, el cambio más reciente primero.</summary>
    Task<IReadOnlyList<PlatformSettingChangeDto>> ListByKeyAsync(string key, CancellationToken ct = default);
}
