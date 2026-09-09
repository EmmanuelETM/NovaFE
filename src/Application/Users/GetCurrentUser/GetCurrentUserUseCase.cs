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
/// <item>humano del dashboard (<c>user:{id}</c>) → su <see cref="PlatformUser"/>;</item>
/// <item>cualquier otro esquema (API key, header de dev, operador) → un perfil
/// derivado de los claims, para que el dashboard igual pinte algo.</item>
/// </list>
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

        if (principalId.StartsWith(PlatformUserPrefix, StringComparison.Ordinal)
            && Guid.TryParse(principalId[PlatformUserPrefix.Length..], out var userId))
        {
            var user = await platformUsers.FindByIdAsync(userId, ct);
            if (user is null || !user.IsActive)
                return PlatformUserErrors.NotFound(userId);

            return new UserProfileDto(
                userId.ToString(),
                user.Email,
                user.Role,
                user.TenantId,
                await TenantNameAsync(user.TenantId, ct));
        }

        // API key / X-Tenant-Id de dev / operador: no hay PlatformUser detrás.
        var role = currentUser.Roles.FirstOrDefault() ?? "consultor";
        var tenantId = currentTenant.TenantId;

        return new UserProfileDto(
            principalId,
            currentUser.UserName,
            role,
            tenantId,
            await TenantNameAsync(tenantId, ct));
    }

    private async Task<string?> TenantNameAsync(Guid? tenantId, CancellationToken ct)
        => tenantId is { } id ? (await tenants.GetByIdAsync(id, ct))?.LegalName : null;
}
