using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Domain.Tenants;

namespace NovaFE.Application.Organizations.AssignTenant;

public sealed class AssignTenantToOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationRepository organizations,
    ITenantRepository tenants)
    : CommandUseCase<AssignTenantToOrganizationCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(
        AssignTenantToOrganizationCommand request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        var tenant = await tenants.GetByIdAsync(request.TenantId, ct);
        if (tenant is null)
            return TenantErrors.NotFound(request.TenantId);

        if (tenant.OrganizationId is { } currentOrgId && currentOrgId != request.OrganizationId)
            return TenantErrors.AlreadyAssignedToOrganization(tenant.Id, currentOrgId);

        tenant.AssignToOrganization(request.OrganizationId);
        await tenants.UpdateAsync(tenant, ct);

        return Result.Success;
    }
}
