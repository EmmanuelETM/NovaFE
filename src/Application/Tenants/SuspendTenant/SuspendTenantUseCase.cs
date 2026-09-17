using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.SuspendTenant;

public sealed class SuspendTenantUseCase(
    ILoggerFactory loggerFactory,
    ITenantRepository tenants)
    : CommandUseCase<SuspendTenantCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(SuspendTenantCommand request, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
            return TenantErrors.NotFound(request.TenantId);

        var suspended = tenant.Suspend();
        if (suspended.IsError)
            return suspended.Errors;

        await tenants.UpdateAsync(tenant, ct);
        return Result.Success;
    }
}
