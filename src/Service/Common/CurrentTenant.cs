using NovaFE.Application.Common.Interfaces;

namespace NovaFE.Service.Common;

/// <summary>
/// Tenant de la petición actual. Hoy se resuelve del header <c>X-Tenant-Id</c>;
/// cuando exista autenticación por API key se resolverá de la key sin cambiar
/// nada más. Lo llena <c>TenantResolutionMiddleware</c> al inicio de la petición.
/// </summary>
internal sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }

    public bool HasValue => TenantId.HasValue;

    public Guid Require() => TenantId
        ?? throw new InvalidOperationException(
            "La operación requiere un tenant y la petición en curso no tiene ninguno.");

    internal void Set(Guid tenantId) => TenantId = tenantId;
}
