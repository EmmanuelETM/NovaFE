using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ChangeUserRole;

/// <summary>
/// Cambia el rol de un usuario **en un tenant puntual**. Recurso de operador.
/// Un usuario multi-tenant (Fase 2) tiene un rol por tenant — el cambio toca
/// únicamente la fila de <see cref="TenantMember"/> de
/// <see cref="ChangeUserRoleCommand.TenantScope"/>, nunca el
/// <see cref="PlatformUser.Role"/> global (vestigial, solo refleja el rol del
/// primer tenant al que se dio de alta a esta persona). Un usuario sin
/// membresía en ese tenant se reporta como no encontrado (no se filtra que
/// existe bajo otro contribuyente) — espejo de <c>RevokeUserUseCase</c>.
/// </summary>
public sealed class ChangeUserRoleUseCase(
    ILoggerFactory loggerFactory,
    IValidator<ChangeUserRoleCommand> validator,
    IPlatformUserRepository users,
    ITenantMemberRepository tenantMembers)
    : CommandUseCase<ChangeUserRoleCommand, PlatformUserDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<PlatformUserDto>> ExecuteCore(
        ChangeUserRoleCommand request,
        CancellationToken ct)
    {
        var user = await users.GetAsync(request.UserId, ct);
        if (user is null)
            return PlatformUserErrors.NotFound(request.UserId);

        var member = await tenantMembers.GetAsync(request.TenantScope, user.Id, ct);
        if (member is null)
            return PlatformUserErrors.NotFound(request.UserId);

        var role = PlatformRole.FromName(request.Role.Trim());

        var changed = member.ChangeRole(role);
        if (changed.IsError)
            return changed.Errors;

        await tenantMembers.UpdateAsync(member, ct);

        return new PlatformUserDto(
            user.Id, user.Email, member.Role.Name, member.TenantId, user.AuthUserId is not null, user.RevokedAt, member.CreatedAt);
    }
}
