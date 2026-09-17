using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>
/// Write side (EF Core) de las membresías de tenant (<see cref="TenantMember"/>).
/// Sin caso de uso propio todavía en Fase 1 — lo usa el backfill de organizaciones;
/// la gestión self-service (invitar/quitar/cambiar rol) es Fase 2, cuando el
/// flujo de autenticación humana deje de leer <c>PlatformUser.TenantId</c>/<c>Role</c>.
/// </summary>
public interface ITenantMemberRepository
{
    Task<TenantMember?> GetAsync(Guid tenantId, Guid platformUserId, CancellationToken ct = default);

    Task AddAsync(TenantMember member, CancellationToken ct = default);

    Task UpdateAsync(TenantMember member, CancellationToken ct = default);
}
