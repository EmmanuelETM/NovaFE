using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ListOrganizationMembers;

public sealed class ListOrganizationMembersUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationReadRepository organizations)
    : QueryUseCase<ListOrganizationMembersQuery, IReadOnlyList<OrganizationMemberDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<IReadOnlyList<OrganizationMemberDto>>> ExecuteCore(
        ListOrganizationMembersQuery request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        return ErrorOrFactory.From(await organizations.ListMembersAsync(request.OrganizationId, ct));
    }
}
