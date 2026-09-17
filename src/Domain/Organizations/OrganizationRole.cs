using NovaFE.Domain.Common;

namespace NovaFE.Domain.Organizations;

/// <summary>
/// Rol de un usuario dentro de una <see cref="Organization"/> (nivel
/// organización: facturación, invitar miembros, crear/asociar contribuyentes).
/// <b>Ortogonal</b> a <see cref="Tenants.ApiKeyRole"/>/<see cref="Users.PlatformRole"/>
/// (nivel contribuyente/proyecto: configuración fiscal, emisión, consulta) — un
/// <c>member</c> de la organización puede ser <c>admin_tenant</c> de uno de sus
/// contribuyentes sin ser <c>owner</c>/<c>admin</c> de la organización, y
/// viceversa. No se colapsan en un solo enum a propósito.
/// </summary>
public sealed record OrganizationRole(int Id, string Name) : Enumeration<OrganizationRole>(Id, Name)
{
    /// <summary>Dueño de la cuenta: facturación, borrar la organización, todo lo de <c>Admin</c>.</summary>
    public static readonly OrganizationRole Owner = new(1, "owner");

    /// <summary>Invitar/quitar miembros, crear y asociar contribuyentes.</summary>
    public static readonly OrganizationRole Admin = new(2, "admin");

    /// <summary>Ve la organización y sus contribuyentes; no administra la membresía.</summary>
    public static readonly OrganizationRole Member = new(3, "member");
}
