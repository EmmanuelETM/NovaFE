using ErrorOr;
using FluentValidation;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Domain.Users;
using Microsoft.Extensions.Logging;

namespace NovaFE.Application.Organizations.RegisterOrganization;

/// <summary>
/// Onboarding estructural (Fase 2): crea la organización y, si viene
/// <see cref="RegisterOrganizationCommand.OwnerEmail"/>, da de alta al dueño en
/// el mismo paso — el operador no tiene que orquestar tres llamadas separadas.
/// </summary>
public sealed class RegisterOrganizationUseCase(
    ILoggerFactory loggerFactory,
    IValidator<RegisterOrganizationCommand> validator,
    IOrganizationRepository organizations,
    IOrganizationMemberRepository members,
    IPlatformUserRepository users)
    : CommandUseCase<RegisterOrganizationCommand, Guid>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<Guid>> ExecuteCore(
        RegisterOrganizationCommand request,
        CancellationToken ct)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await organizations.SlugExistsAsync(slug, ct))
            return OrganizationErrors.SlugAlreadyRegistered(slug);

        var plan = OrganizationPlan.GetAll()
            .FirstOrDefault(p => string.Equals(p.Name, request.Plan.Trim(), StringComparison.OrdinalIgnoreCase));
        if (plan is null)
            return OrganizationErrors.UnknownPlan(request.Plan);

        var organization = Organization.Register(request.Name, slug, plan);
        await organizations.AddAsync(organization, ct);

        if (!string.IsNullOrWhiteSpace(request.OwnerEmail))
        {
            var ownerResult = await FindOrCreateOwnerAsync(request.OwnerEmail, ct);
            if (ownerResult.IsError)
                return ownerResult.Errors;

            var member = OrganizationMember.Create(organization.Id, ownerResult.Value.Id, OrganizationRole.Owner);
            await members.AddAsync(member, ct);
        }

        return organization.Id;
    }

    private async Task<ErrorOr<PlatformUser>> FindOrCreateOwnerAsync(string email, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var existing = await users.GetByEmailAsync(normalized, ct);
        if (existing is not null)
            return existing;

        var created = PlatformUser.CreateOrganizationMember(normalized);
        if (created.IsError)
            return created.Errors;

        await users.AddAsync(created.Value, ct);
        return created.Value;
    }
}
