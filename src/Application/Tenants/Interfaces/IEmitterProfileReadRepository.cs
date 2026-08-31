using NovaFE.Application.Tenants.Contracts;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>Read side (Dapper) del perfil fiscal del emisor.</summary>
public interface IEmitterProfileReadRepository
{
    Task<EmitterProfileDto?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
