using NovaFE.Application.Users.Contracts;

namespace NovaFE.Application.Users.Interfaces;

/// <summary>Read side (Dapper) de los usuarios de la plataforma.</summary>
public interface IPlatformUserReadRepository
{
    /// <summary>
    /// Resuelve al usuario para autenticar. Busca primero por
    /// <paramref name="authUserId"/> (si viene); si no da resultado, por
    /// <paramref name="email"/> (normalizado). Cruza tenants a propósito: todavía
    /// no hay tenant en la petición.
    /// </summary>
    Task<PlatformUserLookup?> ResolveAsync(string? authUserId, string email, CancellationToken ct = default);

    /// <summary>El usuario <paramref name="id"/> como DTO, o <c>null</c>.</summary>
    Task<PlatformUserDto?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Los usuarios de un contribuyente, el más reciente primero.</summary>
    Task<IReadOnlyList<PlatformUserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Los operadores del SaaS (rol <c>admin_sistema</c>), el más reciente primero.</summary>
    Task<IReadOnlyList<PlatformUserDto>> ListOperatorsAsync(CancellationToken ct = default);

    /// <summary>
    /// El tenant activo pedido explícitamente (<c>X-Acting-Tenant-Id</c>) y el
    /// rol efectivo del usuario ahí — directo por <c>tenant_members</c>, o
    /// heredado como <c>admin_tenant</c> si es <c>owner</c>/<c>admin</c> de la
    /// organización dueña. <c>null</c> si no tiene acceso, el tenant no existe,
    /// o el tenant/su organización están suspendidos (Fase 2).
    /// </summary>
    Task<TenantAccessLookup?> ResolveTenantAccessAsync(
        Guid platformUserId, Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// El tenant activo por defecto cuando no viene <c>X-Acting-Tenant-Id</c>:
    /// el primero (por antigüedad) al que el usuario tiene acceso directo, o si
    /// no, el primero heredado de alguna organización donde sea
    /// <c>owner</c>/<c>admin</c>. <c>null</c> si no tiene ningún tenant
    /// disponible todavía (usuario recién invitado a una organización sin
    /// tenants) — no es un error, el perfil queda sin tenant activo.
    /// </summary>
    Task<TenantAccessLookup?> ResolveDefaultTenantAccessAsync(Guid platformUserId, CancellationToken ct = default);

    /// <summary>Las organizaciones del usuario y, dentro de cada una, los tenants a los que tiene acceso.</summary>
    Task<IReadOnlyList<OrganizationMembershipLookup>> ListOrganizationMembershipsAsync(
        Guid platformUserId, CancellationToken ct = default);

    /// <summary>
    /// Los tenants a los que el usuario llega por <c>tenant_members</c> directo,
    /// sin pasar por ninguna organización — para no dejarlos invisibles cuando
    /// el tenant no está cubierto por ninguna de sus organizaciones. El listado
    /// completo, no un solo "default": si hay más de uno, todos deben ser
    /// alcanzables desde el dashboard.
    /// </summary>
    Task<IReadOnlyList<OrganizationTenantLookup>> ListDirectTenantAccessAsync(
        Guid platformUserId, CancellationToken ct = default);
}

/// <summary>Lo mínimo para autenticar y resolver el tenant/rol de un humano.</summary>
public sealed record PlatformUserLookup(
    Guid Id,
    Guid? TenantId,
    string Role,
    string Email,
    string? AuthUserId,
    DateTimeOffset? RevokedAt);

/// <summary>El tenant resuelto para la petición y el rol efectivo del usuario ahí.</summary>
public sealed record TenantAccessLookup(Guid TenantId, string Role);

/// <summary>Una organización del usuario, su rol ahí, y sus tenants accesibles.</summary>
public sealed record OrganizationMembershipLookup(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string OrganizationPlan,
    string OrganizationStatus,
    string OrganizationRole,
    IReadOnlyList<OrganizationTenantLookup> Tenants);

/// <summary>Un tenant dentro de una organización del usuario, con su rol efectivo ahí.</summary>
public sealed record OrganizationTenantLookup(Guid TenantId, string TenantName, string Role);
