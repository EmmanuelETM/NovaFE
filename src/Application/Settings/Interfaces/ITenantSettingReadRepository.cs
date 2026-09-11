using NovaFE.Application.Settings.Contracts;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Read side (Dapper) de los overrides de settings de tenant. El <c>tenantId</c> va
/// explícito en el <c>WHERE</c> como defensa en profundidad (la RLS de
/// <c>tenant_settings</c> ya acota en producción; el filtro es la garantía en
/// local/tests donde la app conecta como superusuario).
/// </summary>
public interface ITenantSettingReadRepository
{
    /// <summary>Los overrides (ambiente agnóstico) de un tenant, con su "quién/cuándo".</summary>
    Task<IReadOnlyList<TenantSettingRecord>> ListAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>La bitácora de una clave para un tenant, la más reciente primero.</summary>
    Task<IReadOnlyList<TenantSettingChangeDto>> ListChangesByKeyAsync(
        Guid tenantId, string key, CancellationToken ct = default);
}
