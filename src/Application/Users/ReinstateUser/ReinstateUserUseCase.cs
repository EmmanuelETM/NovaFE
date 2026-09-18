using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ReinstateUser;

/// <summary>
/// Reactiva a un usuario. Recurso de operador. La idempotencia estricta
/// (reactivar a uno no revocado → conflicto) la aplica el dominio. Espejo
/// exacto de <c>RevokeUserUseCase</c>: con <see cref="ReinstateUserCommand.TenantScope"/>
/// reactiva la fila de <see cref="TenantMember"/> de ese tenant puntual; sin
/// él (ruta de operador), reactiva <see cref="PlatformUser.RevokedAt"/> global.
/// </summary>
public sealed class ReinstateUserUseCase(
    ILoggerFactory loggerFactory,
    IPlatformUserRepository users,
    ITenantMemberRepository tenantMembers)
    : CommandUseCase<ReinstateUserCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        ReinstateUserCommand request,
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

            var reinstatedMember = member.Reinstate();
            if (reinstatedMember.IsError)
                return reinstatedMember.Errors;

            await tenantMembers.UpdateAsync(member, ct);
            return Result.Success;
        }

        if (user.TenantId is not null) // ruta de operador, pero el usuario es de un tenant
            return PlatformUserErrors.NotFound(request.UserId);

        var reinstated = user.Reinstate();
        if (reinstated.IsError)
            return reinstated.Errors;

        await users.UpdateAsync(user, ct);
        return Result.Success;
    }
}
