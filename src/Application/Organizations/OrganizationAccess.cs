using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Organizations;

/// <summary>
/// Guarda de acceso a nivel organización, reusada por los casos de uso de
/// membresía (Fase 2): el operador siempre puede; un humano del dashboard
/// puede si es <c>owner</c>/<c>admin</c> de <b>esa</b> organización puntual —
/// una comprobación por recurso de ruta, no una política declarativa de
/// ASP.NET Core (el rol de organización varía por organización, no es un
/// claim fijo del principal como <c>tenant_id</c>).
/// </summary>
internal static class OrganizationAccess
{
    private const string PlatformUserPrefix = "user:";

    public static bool IsOperator(ICurrentUser currentUser) =>
        currentUser.IsInRole(PlatformRole.AdminSistema.Name);

    /// <summary>El <see cref="PlatformUser.Id"/> del principal actual, si vino del dashboard.</summary>
    public static Guid? PlatformUserId(ICurrentUser currentUser) =>
        currentUser.Id is { } id
        && id.StartsWith(PlatformUserPrefix, StringComparison.Ordinal)
        && Guid.TryParse(id[PlatformUserPrefix.Length..], out var userId)
            ? userId
            : null;

    /// <summary>
    /// El operador siempre pasa. Si no, hace falta ser <c>owner</c>/<c>admin</c>
    /// de <paramref name="organizationId"/> — nunca "de una organización
    /// cualquiera": la membresía se resuelve para ese id puntual. Para
    /// invitar/cambiar rol/quitar miembros.
    /// </summary>
    public static async Task<bool> CanManageMembersAsync(
        ICurrentUser currentUser,
        IOrganizationMemberRepository members,
        Guid organizationId,
        CancellationToken ct)
    {
        if (IsOperator(currentUser))
            return true;

        var membership = await MembershipAsync(currentUser, members, organizationId, ct);
        return membership is not null
            && (membership.Role == OrganizationRole.Owner || membership.Role == OrganizationRole.Admin);
    }

    /// <summary>Cualquier miembro (o el operador) puede ver la lista de miembros de su organización.</summary>
    public static async Task<bool> CanViewAsync(
        ICurrentUser currentUser,
        IOrganizationMemberRepository members,
        Guid organizationId,
        CancellationToken ct)
    {
        if (IsOperator(currentUser))
            return true;

        return await MembershipAsync(currentUser, members, organizationId, ct) is not null;
    }

    private static Task<OrganizationMember?> MembershipAsync(
        ICurrentUser currentUser,
        IOrganizationMemberRepository members,
        Guid organizationId,
        CancellationToken ct)
    {
        var userId = PlatformUserId(currentUser);
        return userId is null
            ? Task.FromResult<OrganizationMember?>(null)
            : members.GetAsync(organizationId, userId.Value, ct);
    }
}
