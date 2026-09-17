using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ChangeOrganizationMemberRole;

public sealed class ChangeOrganizationMemberRoleUseCase(
    ILoggerFactory loggerFactory,
    IValidator<ChangeOrganizationMemberRoleCommand> validator,
    ICurrentUser currentUser,
    IOrganizationMemberRepository members,
    IPlatformUserRepository users)
    : CommandUseCase<ChangeOrganizationMemberRoleCommand, OrganizationMemberDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<OrganizationMemberDto>> ExecuteCore(
        ChangeOrganizationMemberRoleCommand request,
        CancellationToken ct)
    {
        if (!await OrganizationAccess.CanManageMembersAsync(currentUser, members, request.OrganizationId, ct))
            return OrganizationErrors.NotAllowed;

        var member = await members.GetAsync(request.OrganizationId, request.PlatformUserId, ct);
        if (member is null)
            return OrganizationErrors.MemberNotFound(request.PlatformUserId);

        var newRole = OrganizationRole.FromName(request.Role.Trim());

        if (member.Role == OrganizationRole.Owner
            && newRole != OrganizationRole.Owner
            && await members.CountByRoleAsync(request.OrganizationId, OrganizationRole.Owner, ct) <= 1)
        {
            return OrganizationErrors.CannotRemoveLastOwner;
        }

        member.ChangeRole(newRole);
        await members.UpdateAsync(member, ct);

        var user = await users.GetAsync(request.PlatformUserId, ct);

        return new OrganizationMemberDto(member.PlatformUserId, user?.Email ?? string.Empty, member.Role.Name, member.CreatedAt);
    }
}
