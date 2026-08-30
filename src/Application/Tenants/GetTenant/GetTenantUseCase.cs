using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Tenants.GetTenant;

public sealed class GetTenantUseCase(
    ILoggerFactory loggerFactory,
    ITenantReadRepository tenants)
    : QueryUseCase<GetTenantQuery, TenantDetail>(loggerFactory)
{
    protected override async Task<ErrorOr<TenantDetail>> ExecuteCore(
        GetTenantQuery request,
        CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(request.Id, ct);

        return tenant is null
            ? TenantErrors.NotFound(request.Id)
            : tenant;
    }
}
