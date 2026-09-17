using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.RemoveOrganizationMember;

public sealed class RemoveOrganizationMemberUseCase(
    ILoggerFactory loggerFactory,
    ICurrentUser currentUser,
    IOrganizationMemberRepository members)
    : CommandUseCase<RemoveOrganizationMemberCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        RemoveOrganizationMemberCommand request,
        CancellationToken ct)
    {
        if (!await OrganizationAccess.CanManageMembersAsync(currentUser, members, request.OrganizationId, ct))
            return OrganizationErrors.NotAllowed;

        var member = await members.GetAsync(request.OrganizationId, request.PlatformUserId, ct);
        if (member is null)
            return OrganizationErrors.MemberNotFound(request.PlatformUserId);

        if (member.Role == OrganizationRole.Owner
            && await members.CountByRoleAsync(request.OrganizationId, OrganizationRole.Owner, ct) <= 1)
        {
            return OrganizationErrors.CannotRemoveLastOwner;
        }

        await members.RemoveAsync(member, ct);

        return Result.Success;
    }
}
