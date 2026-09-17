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
/// <para>
/// Fase 2: además de <see cref="PlatformUser"/> (identidad), crea la fila de
/// <see cref="TenantMember"/> (acceso real) — el login del dashboard ya no lee
/// <c>PlatformUser.TenantId</c>/<c>Role</c>, resuelve el tenant/rol efectivo
/// desde <c>tenant_members</c>. Sin esta fila, el usuario quedaría dado de alta
/// pero sin poder entrar a ningún tenant.
/// </para>
/// </summary>
public sealed class ProvisionTenantUserUseCase(
    ILoggerFactory loggerFactory,
    IValidator<ProvisionTenantUserCommand> validator,
    ITenantReadRepository tenants,
    IPlatformUserRepository users,
    ITenantMemberRepository tenantMembers)
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

        var role = PlatformRole.FromName(request.Role.Trim());

        var created = PlatformUser.CreateTenantUser(request.Email, request.TenantId, role);
        if (created.IsError)
            return created.Errors;

        await users.AddAsync(created.Value, ct);

        var member = TenantMember.Create(request.TenantId, created.Value.Id, role);
        if (member.IsError)
            return member.Errors;

        await tenantMembers.AddAsync(member.Value, ct);

        return UserDtoMapper.ToDto(created.Value);
    }
}
