using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.UpdateOrganizationPlan;

public sealed class UpdateOrganizationPlanUseCase(
    ILoggerFactory loggerFactory,
    IValidator<UpdateOrganizationPlanCommand> validator,
    IOrganizationRepository organizations)
    : CommandUseCase<UpdateOrganizationPlanCommand>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<Success>> ExecuteCore(UpdateOrganizationPlanCommand request, CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.OrganizationId, ct);
        if (organization is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        organization.ChangePlan(OrganizationPlan.FromName(request.Plan));

        await organizations.UpdateAsync(organization, ct);
        return Result.Success;
    }
}
