using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>
/// Write side (EF Core). Loads and persists the <see cref="Tenant"/> aggregate.
/// Reads for queries go through <see cref="ITenantReadRepository"/>.
/// </summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> RncExistsAsync(string rnc, CancellationToken ct = default);

    Task AddAsync(Tenant tenant, CancellationToken ct = default);
}
