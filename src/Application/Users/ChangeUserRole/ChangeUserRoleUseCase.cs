using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ChangeUserRole;

/// <summary>
/// Cambia el rol de un usuario de un contribuyente. Recurso de operador. Un
/// usuario fuera del <see cref="ChangeUserRoleCommand.TenantScope"/> se reporta
/// como no encontrado (no se filtra que existe bajo otro contribuyente) — espejo
/// de <c>RevokeUserUseCase</c>.
/// </summary>
public sealed class ChangeUserRoleUseCase(
    ILoggerFactory loggerFactory,
    IValidator<ChangeUserRoleCommand> validator,
    IPlatformUserRepository users)
    : CommandUseCase<ChangeUserRoleCommand, PlatformUserDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<PlatformUserDto>> ExecuteCore(
        ChangeUserRoleCommand request,
        CancellationToken ct)
    {
        var user = await users.GetAsync(request.UserId, ct);
        if (user is null || user.TenantId != request.TenantScope)
            return PlatformUserErrors.NotFound(request.UserId);

        var changed = user.ChangeRole(PlatformRole.FromName(request.Role.Trim()));
        if (changed.IsError)
            return changed.Errors;

        await users.UpdateAsync(user, ct);

        return UserDtoMapper.ToDto(user);
    }
}
