using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.RevokeUser;

/// <summary>
/// Revoca a un usuario. Recurso de operador. La idempotencia estricta
/// (revocar dos veces → conflicto) la aplica el dominio.
/// <para>
/// Con <see cref="RevokeUserCommand.TenantScope"/> (ruta
/// <c>/tenants/{id}/users/{userId}</c>) revoca la fila de
/// <see cref="TenantMember"/> de <b>ese</b> tenant puntual — a alguien con
/// acceso a varios tenants no se le toca el resto. Un usuario sin membresía
/// ahí se reporta como no encontrado, no se filtra que existe bajo otro
/// contribuyente.
/// </para>
/// <para>
/// Sin <see cref="RevokeUserCommand.TenantScope"/> (ruta
/// <c>/operator-users/{userId}</c>) revoca <see cref="PlatformUser.RevokedAt"/>,
/// que sí es global — correcto ahí porque un operador no tiene tenants.
/// </para>
/// </summary>
public sealed class RevokeUserUseCase(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider,
    IPlatformUserRepository users,
    ITenantMemberRepository tenantMembers)
    : CommandUseCase<RevokeUserCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        RevokeUserCommand request,
        CancellationToken ct)
    {
        var user = await users.GetAsync(request.UserId, ct);
        if (user is null)
            return PlatformUserErrors.NotFound(request.UserId);

        if (request.TenantScope is { } tenantScope)
        {
            var member = await tenantMembers.GetAsync(tenantScope, user.Id, ct);
            if (member is null)
                return PlatformUserErrors.NotFound(request.UserId);

            var revokedMember = member.Revoke(timeProvider.GetUtcNow());
            if (revokedMember.IsError)
                return revokedMember.Errors;

            await tenantMembers.UpdateAsync(member, ct);
            return Result.Success;
        }

        if (user.TenantId is not null) // ruta de operador, pero el usuario es de un tenant
            return PlatformUserErrors.NotFound(request.UserId);

        var revoked = user.Revoke(timeProvider.GetUtcNow());
        if (revoked.IsError)
            return revoked.Errors;

        await users.UpdateAsync(user, ct);
        return Result.Success;
    }
}
