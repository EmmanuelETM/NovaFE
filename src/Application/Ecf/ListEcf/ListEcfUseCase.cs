using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Ecf.ListEcf;

public sealed class ListEcfUseCase(
    ILoggerFactory loggerFactory,
    ICurrentTenant currentTenant,
    IEcfReadRepository ecf)
    : QueryUseCase<ListEcfQuery, PagedResult<EcfSummaryDto>>(loggerFactory)
{
    protected override async Task<ErrorOr<PagedResult<EcfSummaryDto>>> ExecuteCore(
        ListEcfQuery request, CancellationToken ct)
    {
        if (currentTenant.TenantId is not { } tenantId)
            return Errors.Auth.TenantNotResolved;

        return await ecf.ListAsync(tenantId, request.ToFilter(), ct);
    }
}
