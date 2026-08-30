using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.ListTenants;

public sealed class ListTenantsUseCase(
    ILoggerFactory loggerFactory,
    ITenantReadRepository tenants)
    : QueryUseCase<ListTenantsQuery, PagedResult<TenantSummary>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<TenantSummary>>> ExecuteCore(
        ListTenantsQuery request,
        CancellationToken ct)
    {
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();

        return await tenants.ListAsync(request.Page, request.PageSize, search, ct);
    }
}
