using NovaFE.Domain.Common;
using NovaFE.Domain.Dgii;

namespace NovaFE.Application.Dgii.Interfaces;

/// <summary>
/// Caché efímera del token de la DGII, por (tenant, ambiente). Hoy es
/// <c>IDistributedCache</c> en memoria; con Redis funciona igual (ver
/// <c>docs/redis.md</c>). El token <b>nunca</b> se persiste en base de datos.
/// </summary>
public interface IDgiiTokenCache
{
    Task<AuthenticationToken?> GetAsync(Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default);

    Task SetAsync(Guid tenantId, DgiiEnvironment environment, AuthenticationToken token, CancellationToken ct = default);

    Task RemoveAsync(Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default);
}
