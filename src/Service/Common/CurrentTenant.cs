using NovaFE.Application.Common.Interfaces;
using NovaFE.Domain.Common;

namespace NovaFE.Service.Common;

/// <summary>
/// Tenant (y ambiente) de la petición actual. Lo resuelve
/// <c>TenantResolutionMiddleware</c> de los claims del principal autenticado: la
/// API key lleva el tenant y su ambiente; el header <c>X-Tenant-Id</c> de
/// Development lleva solo el tenant.
/// </summary>
internal sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }

    public DgiiEnvironment? Environment { get; private set; }

    public bool HasValue => TenantId.HasValue;

    public Guid Require() => TenantId
        ?? throw new InvalidOperationException(
            "La operación requiere un tenant y la petición en curso no tiene ninguno.");

    internal void Set(Guid tenantId, DgiiEnvironment? environment = null)
    {
        TenantId = tenantId;
        Environment = environment;
    }
}
