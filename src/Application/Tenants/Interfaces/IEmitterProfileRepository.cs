using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.Interfaces;

/// <summary>
/// Write side (EF Core) del <see cref="EmitterProfile"/>. También lo lee el pipeline
/// de emisión (Módulo 12) cuando necesita el agregado para armar el bloque
/// <c>&lt;Emisor&gt;</c>; las consultas de solo lectura van por
/// <see cref="IEmitterProfileReadRepository"/>.
/// </summary>
public interface IEmitterProfileRepository
{
    Task<EmitterProfile?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);

    Task AddAsync(EmitterProfile profile, CancellationToken ct = default);

    Task UpdateAsync(EmitterProfile profile, CancellationToken ct = default);
}
