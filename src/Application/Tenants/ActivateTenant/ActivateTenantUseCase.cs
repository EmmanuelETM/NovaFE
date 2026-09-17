using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Tenants.ActivateTenant;

public sealed class ActivateTenantUseCase(
    ILoggerFactory loggerFactory,
    ITenantRepository tenants)
    : CommandUseCase<ActivateTenantCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(ActivateTenantCommand request, CancellationToken ct)
    {
        var tenant = await tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
            return TenantErrors.NotFound(request.TenantId);

        var activated = tenant.Activate();
        if (activated.IsError)
            return activated.Errors;

        await tenants.UpdateAsync(tenant, ct);
        return Result.Success;
    }
}
