using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Users.ProvisionTenantUser;

/// <summary>
/// Da de alta a un empleado de un contribuyente. Recurso de operador. El alta es
/// por correo; el <c>auth_user_id</c> de Better Auth se enlaza en el primer login.
/// </summary>
public sealed class ProvisionTenantUserUseCase(
    ILoggerFactory loggerFactory,
    IValidator<ProvisionTenantUserCommand> validator,
    ITenantReadRepository tenants,
    IPlatformUserRepository users)
    : CommandUseCase<ProvisionTenantUserCommand, PlatformUserDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<PlatformUserDto>> ExecuteCore(
        ProvisionTenantUserCommand request,
        CancellationToken ct)
    {
        if (await tenants.GetByIdAsync(request.TenantId, ct) is null)
            return TenantErrors.NotFound(request.TenantId);

        var email = request.Email.Trim().ToLowerInvariant();

        if (await users.GetByEmailAsync(email, ct) is not null)
            return PlatformUserErrors.EmailAlreadyProvisioned(email);

        var created = PlatformUser.CreateTenantUser(
            request.Email, request.TenantId, PlatformRole.FromName(request.Role.Trim()));
        if (created.IsError)
            return created.Errors;

        await users.AddAsync(created.Value, ct);

        return UserDtoMapper.ToDto(created.Value);
    }
}
