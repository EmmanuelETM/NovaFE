using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.RevokeUser;

/// <summary>
/// Revoca a un usuario del dashboard. Recurso de operador. La idempotencia
/// estricta (revocar dos veces → conflicto) la aplica el dominio. Un usuario
/// fuera del <see cref="RevokeUserCommand.TenantScope"/> (sin fila de
/// <see cref="Domain.Tenants.TenantMember"/> ahí — Fase 2, no
/// <c>PlatformUser.TenantId</c>, vestigial) se reporta como no encontrado, no
/// se filtra que existe bajo otro contribuyente.
/// <para>
/// <b>Ojo:</b> esto revoca <see cref="PlatformUser.RevokedAt"/>, que es
/// <b>global</b> — a alguien con acceso a varios tenants lo saca de todos, no
/// solo de <see cref="RevokeUserCommand.TenantScope"/>. Revocación por tenant
/// puntual (dejando el resto de sus accesos intactos) es trabajo pendiente.
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

        var authorized = request.TenantScope is { } tenantScope
            ? await tenantMembers.GetAsync(tenantScope, user.Id, ct) is not null
            : user.TenantId is null; // ruta de operador: TenantScope nulo

        if (!authorized)
            return PlatformUserErrors.NotFound(request.UserId);

        var revoked = user.Revoke(timeProvider.GetUtcNow());
        if (revoked.IsError)
            return revoked.Errors;

        await users.UpdateAsync(user, ct);
        return Result.Success;
    }
}
