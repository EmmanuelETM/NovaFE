using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Organizations.UnassignTenant;

public sealed class UnassignTenantFromOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationRepository organizations,
    ITenantRepository tenants)
    : CommandUseCase<UnassignTenantFromOrganizationCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        UnassignTenantFromOrganizationCommand request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        var tenant = await tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
            return TenantErrors.NotFound(request.TenantId);

        if (tenant.OrganizationId != request.OrganizationId)
            return TenantErrors.NotOwnedByOrganization(request.TenantId, request.OrganizationId);

        tenant.UnassignFromOrganization();
        await tenants.UpdateAsync(tenant, ct);

        return Result.Success;
    }
}
