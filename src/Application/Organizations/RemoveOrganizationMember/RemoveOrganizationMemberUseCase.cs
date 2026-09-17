using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.RemoveOrganizationMember;

public sealed class RemoveOrganizationMemberUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationMemberRepository members)
    : CommandUseCase<RemoveOrganizationMemberCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        RemoveOrganizationMemberCommand request,
        CancellationToken ct)
    {
        var member = await members.GetAsync(request.OrganizationId, request.PlatformUserId, ct);
        if (member is null)
            return OrganizationErrors.MemberNotFound(request.PlatformUserId);

        await members.RemoveAsync(member, ct);

        return Result.Success;
    }
}
