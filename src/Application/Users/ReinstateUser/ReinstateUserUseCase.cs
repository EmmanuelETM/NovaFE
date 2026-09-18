using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ReinstateUser;

/// <summary>
/// Reactiva a un usuario del dashboard. Recurso de operador. La idempotencia
/// estricta (reactivar a uno no revocado → conflicto) la aplica el dominio. Un
/// usuario fuera del <see cref="ReinstateUserCommand.TenantScope"/> (sin fila
/// de <see cref="Domain.Tenants.TenantMember"/> ahí) se reporta como no
/// encontrado — espejo de <c>RevokeUserUseCase</c>, misma salvedad sobre el
/// alcance global de <see cref="PlatformUser.RevokedAt"/>.
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

        var authorized = request.TenantScope is { } tenantScope
            ? await tenantMembers.GetAsync(tenantScope, user.Id, ct) is not null
            : user.TenantId is null; // ruta de operador: TenantScope nulo

        if (!authorized)
            return PlatformUserErrors.NotFound(request.UserId);

        var reinstated = user.Reinstate();
        if (reinstated.IsError)
            return reinstated.Errors;

        await users.UpdateAsync(user, ct);
        return Result.Success;
    }
}
