using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ListOrganizationTenants;

public sealed class ListOrganizationTenantsUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationReadRepository organizations)
    : QueryUseCase<ListOrganizationTenantsQuery, PagedResult<TenantSummaryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<TenantSummaryDto>>> ExecuteCore(
        ListOrganizationTenantsQuery request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        return await organizations.ListTenantsAsync(request.OrganizationId, request.Page, request.PageSize, ct);
    }
}
