using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Organizations.ListOrganizations;

public sealed class ListOrganizationsUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationReadRepository organizations)
    : QueryUseCase<ListOrganizationsQuery, PagedResult<OrganizationSummaryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<OrganizationSummaryDto>>> ExecuteCore(
        ListOrganizationsQuery request,
        CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        return await organizations.ListAsync(request.Page, request.PageSize, search, ct);
    }
}
