namespace NovaFE.Application.Users.Contracts;

/// <summary>
/// Quién es el que hizo la petición, para el dashboard: de acá sale la navegación
/// por rol. Lo devuelve <c>GET /api/v1/users/me</c>.
/// </summary>
/// <param name="TenantId">El tenant <b>activo</b> de esta sesión; <c>null</c> = operador del SaaS, o ningún tenant disponible todavía.</param>
/// <param name="TenantName">Razón social del tenant activo, si aplica.</param>
/// <param name="Organizations">
/// Las organizaciones del usuario y, dentro de cada una, los tenants a los que
/// tiene acceso — para el switcher del dashboard (Fase 3). Vacío para
/// identidades sin <c>PlatformUser</c> detrás (API key, header de dev, operador).
/// </param>
/// <param name="DirectTenants">
/// Los tenants a los que llega por <c>tenant_members</c> directo y que
/// <b>no</b> están ya cubiertos por <see cref="Organizations"/> — el acceso
/// directo a un tenant es independiente de la membresía de organización, y
/// sin este campo esos tenants quedan invisibles en el dashboard cuando su
/// organización cambia o el usuario no es miembro de ella.
/// </param>
public sealed record UserProfileDto(
    string Id,
    string? Email,
    string Role,
    Guid? TenantId,
    string? TenantName,
    IReadOnlyList<UserOrganizationDto> Organizations,
    IReadOnlyList<UserOrganizationTenantDto> DirectTenants);

/// <summary>Una organización del usuario y su rol ahí (<c>owner</c>/<c>admin</c>/<c>member</c>).</summary>
public sealed record UserOrganizationDto(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string Plan,
    string Status,
    string Role,
    IReadOnlyList<UserOrganizationTenantDto> Tenants);

/// <summary>Un tenant accesible dentro de una organización, con el rol efectivo del usuario ahí.</summary>
public sealed record UserOrganizationTenantDto(Guid TenantId, string TenantName, string Role);
