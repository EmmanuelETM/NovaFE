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
/// <para>
/// Un correo que ya es <see cref="PlatformUser"/> (empleado de otro tenant, o
/// miembro de alguna organización) no se rechaza: un mismo usuario puede tener
/// acceso a varios tenants, cada uno con su propio rol — se le agrega la fila
/// de <see cref="TenantMember"/> a la cuenta existente. Solo se rechaza si ya
/// tenía acceso a <b>este</b> tenant puntual.
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
        var role = PlatformRole.FromName(request.Role.Trim());

        var userResult = await FindOrCreateUserAsync(email, request.TenantId, role, ct);
        if (userResult.IsError)
            return userResult.Errors;

        var user = userResult.Value;

        if (await tenantMembers.GetAsync(request.TenantId, user.Id, ct) is not null)
            return TenantMemberErrors.AlreadyMember(email);

        var member = TenantMember.Create(request.TenantId, user.Id, role);
        if (member.IsError)
            return member.Errors;

        await tenantMembers.AddAsync(member.Value, ct);

        return UserDtoMapper.ToDto(user);
    }

    /// <summary>Mismo criterio que <c>AddOrganizationMemberUseCase</c>: un correo desconocido se da de alta en el mismo paso.</summary>
    private async Task<ErrorOr<PlatformUser>> FindOrCreateUserAsync(
        string email, Guid tenantId, PlatformRole role, CancellationToken ct)
    {
        var existing = await users.GetByEmailAsync(email, ct);
        if (existing is not null)
            return existing;

        var created = PlatformUser.CreateTenantUser(email, tenantId, role);
        if (created.IsError)
            return created.Errors;

        await users.AddAsync(created.Value, ct);
        return created.Value;
    }
}
