using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.RevokeUser;

/// <summary>
/// Revoca a un usuario del dashboard. Recurso de operador. La idempotencia
/// estricta (revocar dos veces → conflicto) la aplica el dominio. Un usuario
/// fuera del <see cref="RevokeUserCommand.TenantScope"/> se reporta como no
/// encontrado (no se filtra que existe bajo otro contribuyente).
/// </summary>
public sealed class RevokeUserUseCase(
    ILoggerFactory loggerFactory,
    TimeProvider timeProvider,
    IPlatformUserRepository users)
    : CommandUseCase<RevokeUserCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        RevokeUserCommand request,
        CancellationToken ct)
    {
        var user = await users.GetAsync(request.UserId, ct);
        if (user is null || user.TenantId != request.TenantScope)
            return PlatformUserErrors.NotFound(request.UserId);

        var revoked = user.Revoke(timeProvider.GetUtcNow());
        if (revoked.IsError)
            return revoked.Errors;

        await users.UpdateAsync(user, ct);
        return Result.Success;
    }
}
