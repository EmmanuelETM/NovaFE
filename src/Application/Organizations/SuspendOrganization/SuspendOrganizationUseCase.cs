using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.SuspendOrganization;

public sealed class SuspendOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationRepository organizations)
    : CommandUseCase<SuspendOrganizationCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(SuspendOrganizationCommand request, CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.OrganizationId, ct);
        if (organization is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        var suspended = organization.Suspend();
        if (suspended.IsError)
            return suspended.Errors;

        await organizations.UpdateAsync(organization, ct);
        return Result.Success;
    }
}
