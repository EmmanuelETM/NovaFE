using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Domain.Users;

namespace NovaFE.Application.Organizations.AddOrganizationMember;

public sealed class AddOrganizationMemberUseCase(
    ILoggerFactory loggerFactory,
    IValidator<AddOrganizationMemberCommand> validator,
    ICurrentUser currentUser,
    IOrganizationRepository organizations,
    IOrganizationMemberRepository members,
    IPlatformUserRepository users)
    : CommandUseCase<AddOrganizationMemberCommand, OrganizationMemberDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<OrganizationMemberDto>> ExecuteCore(
        AddOrganizationMemberCommand request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        if (!await OrganizationAccess.CanManageMembersAsync(currentUser, members, request.OrganizationId, ct))
            return OrganizationErrors.NotAllowed;

        var email = request.Email.Trim().ToLowerInvariant();

        var userResult = await FindOrCreateUserAsync(email, ct);
        if (userResult.IsError)
            return userResult.Errors;

        var user = userResult.Value;

        if (await members.GetAsync(request.OrganizationId, user.Id, ct) is not null)
            return OrganizationErrors.MemberAlreadyExists(email);

        var role = OrganizationRole.FromName(request.Role.Trim());
        var member = OrganizationMember.Create(request.OrganizationId, user.Id, role);

        await members.AddAsync(member, ct);

        return new OrganizationMemberDto(user.Id, user.Email, role.Name, member.CreatedAt);
    }

    /// <summary>
    /// Invitar a un correo que todavía no es usuario de la plataforma lo da
    /// de alta en el mismo paso — mismo criterio que <c>ownerEmail</c> al
    /// crear una organización, el operador no tiene que orquestar dos
    /// llamadas separadas.
    /// </summary>
    private async Task<ErrorOr<PlatformUser>> FindOrCreateUserAsync(string email, CancellationToken ct)
    {
        var existing = await users.GetByEmailAsync(email, ct);
        if (existing is not null)
            return existing;

        var created = PlatformUser.CreateOrganizationMember(email);
        if (created.IsError)
            return created.Errors;

        await users.AddAsync(created.Value, ct);
        return created.Value;
    }
}
