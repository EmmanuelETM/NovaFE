using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>Write side (EF Core) de las <see cref="ApiKey"/>.</summary>
public interface IApiKeyRepository
{
    /// <summary>La credencial <paramref name="id"/> del contribuyente <paramref name="tenantId"/>, o <c>null</c>.</summary>
    Task<ApiKey?> GetAsync(Guid id, Guid tenantId, CancellationToken ct = default);

    Task AddAsync(ApiKey key, CancellationToken ct = default);

    Task UpdateAsync(ApiKey key, CancellationToken ct = default);

    /// <summary>
    /// Marca el último uso de una credencial sin cargarla ni tocar su auditoría.
    /// Best-effort: lo llama el autenticador, coalescido en el tiempo.
    /// </summary>
    Task TouchAsync(Guid id, DateTimeOffset usedAt, CancellationToken ct = default);
}
