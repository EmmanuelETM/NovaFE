using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.GetCurrentUser;

/// <summary>
/// Quién hizo la petición, para <c>GET /api/v1/users/me</c>. Responde según el
/// esquema que autenticó:
/// <list type="bullet">
/// <item>humano del dashboard (<c>user:{id}</c>) → su <see cref="PlatformUser"/>
/// + sus organizaciones/tenants (Fase 2);</item>
/// <item>cualquier otro esquema (API key, header de dev, operador) → un perfil
/// derivado de los claims, para que el dashboard igual pinte algo.</item>
/// </list>
/// El tenant/rol <b>activos</b> (<see cref="UserProfileDto.TenantId"/>/
/// <see cref="UserProfileDto.Role"/>) salen siempre de <see cref="ICurrentUser"/>/
/// <see cref="ICurrentTenant"/> — ya resueltos por el pipeline de autenticación
/// (Fase 2: <c>tenant_members</c>/<c>organization_members</c>, no
/// <c>PlatformUser.TenantId</c>/<c>Role</c>, que son vestigiales para un
/// usuario de contribuyente desde esta fase).
/// </summary>
public sealed class GetCurrentUserUseCase(
    ILoggerFactory loggerFactory,
    ICurrentUser currentUser,
    ICurrentTenant currentTenant,
    IPlatformUserReadRepository platformUsers,
    ITenantReadRepository tenants)
    : ParameterlessQueryUseCase<UserProfileDto>(loggerFactory)
{
    private const string PlatformUserPrefix = "user:";

    protected override async Task<ErrorOr<UserProfileDto>> ExecuteCore(NoRequest request, CancellationToken ct)
    {
        var principalId = currentUser.Id;
        if (string.IsNullOrEmpty(principalId))
            return Errors.Auth.NotAuthenticated;

        var role = currentUser.Roles.FirstOrDefault() ?? PlatformRole.Consultor.Name;
        var tenantId = currentTenant.TenantId;
        var tenantName = await TenantNameAsync(tenantId, ct);

        if (principalId.StartsWith(PlatformUserPrefix, StringComparison.Ordinal)
            && Guid.TryParse(principalId[PlatformUserPrefix.Length..], out var userId))
        {
            var user = await platformUsers.FindByIdAsync(userId, ct);
            if (user is null || !user.IsActive)
                return PlatformUserErrors.NotFound(userId);

            return new UserProfileDto(
                userId.ToString(), user.Email, role, tenantId, tenantName, await OrganizationsAsync(userId, ct));
        }

        // API key / X-Tenant-Id de dev / operador: no hay PlatformUser detrás.
        return new UserProfileDto(principalId, currentUser.UserName, role, tenantId, tenantName, []);
    }

    private async Task<IReadOnlyList<UserOrganizationDto>> OrganizationsAsync(Guid userId, CancellationToken ct)
    {
        var memberships = await platformUsers.ListOrganizationMembershipsAsync(userId, ct);

        return [.. memberships.Select(m => new UserOrganizationDto(
            m.OrganizationId,
            m.OrganizationName,
            m.OrganizationSlug,
            m.OrganizationPlan,
            m.OrganizationStatus,
            m.OrganizationRole,
            [.. m.Tenants.Select(t => new UserOrganizationTenantDto(t.TenantId, t.TenantName, t.Role))]))];
    }

    private async Task<string?> TenantNameAsync(Guid? tenantId, CancellationToken ct)
        => tenantId is { } id ? (await tenants.GetByIdAsync(id, ct))?.LegalName : null;
}
