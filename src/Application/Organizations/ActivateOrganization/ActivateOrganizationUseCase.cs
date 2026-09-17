using ErrorOr;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.ActivateOrganization;

public sealed class ActivateOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationRepository organizations)
    : CommandUseCase<ActivateOrganizationCommand>(loggerFactory)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(ActivateOrganizationCommand request, CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.OrganizationId, ct);
        if (organization is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        var activated = organization.Activate();
        if (activated.IsError)
            return activated.Errors;

        await organizations.UpdateAsync(organization, ct);
        return Result.Success;
    }
}
