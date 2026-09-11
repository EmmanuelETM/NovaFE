using NovaFE.Application.Settings.Contracts;

namespace NovaFE.Application.Settings.Interfaces;

/// <summary>
/// Write side (EF Core) de los overrides de settings de tenant. El tenant sale de
/// <c>ICurrentTenant</c> vía el filtro global y el interceptor de estampado, no de
/// un parámetro. La bitácora es <see cref="ITenantSettingChangeLog"/>: el caso de
/// uso los orquesta en una sola transacción.
/// </summary>
public interface ITenantSettingRepository
{
    /// <summary>El override de una clave para el tenant actual (ambiente agnóstico), o null.</summary>
    Task<TenantSettingRecord?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Crea o reemplaza el override de una clave para el tenant actual.</summary>
    Task UpsertAsync(string key, string value, CancellationToken ct = default);

    /// <summary>Quita el override (vuelta al default). Devuelve <c>false</c> si no había.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken ct = default);
}
