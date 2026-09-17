using ErrorOr;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Organizations.GetOrganization;

public sealed class GetOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IOrganizationReadRepository organizations)
    : QueryUseCase<GetOrganizationQuery, OrganizationDto>(loggerFactory)
{
    protected override async Task<ErrorOr<OrganizationDto>> ExecuteCore(
        GetOrganizationQuery request,
        CancellationToken ct)
    {
        var organization = await organizations.GetByIdAsync(request.Id, ct);

        return organization is null
            ? OrganizationErrors.NotFound(request.Id)
            : organization;
    }
}
