using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ListOrganizationTenants;

public sealed class ListOrganizationTenantsUseCase(
    ILoggerFactory loggerFactory,
    ICurrentUser currentUser,
    IOrganizationReadRepository organizations,
    IOrganizationMemberRepository members)
    : QueryUseCase<ListOrganizationTenantsQuery, PagedResult<TenantSummaryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<TenantSummaryDto>>> ExecuteCore(
        ListOrganizationTenantsQuery request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        if (!await OrganizationAccess.CanViewAsync(currentUser, members, request.OrganizationId, ct))
            return OrganizationErrors.NotAllowed;

        return await organizations.ListTenantsAsync(request.OrganizationId, request.Page, request.PageSize, ct);
    }
}
