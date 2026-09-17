using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Organizations.RegisterOrganization;

public sealed class RegisterOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IValidator<RegisterOrganizationCommand> validator,
    IOrganizationRepository organizations)
    : CommandUseCase<RegisterOrganizationCommand, Guid>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<Guid>> ExecuteCore(
        RegisterOrganizationCommand request,
        CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await organizations.SlugExistsAsync(slug, ct))
            return OrganizationErrors.SlugAlreadyRegistered(slug);

        var organization = Organization.Register(request.Name, slug);

        await organizations.AddAsync(organization, ct);

        return organization.Id;
    }
}
