using ErrorOr;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NovaFE.Application.Common;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.AddOrganizationMember;

public sealed class AddOrganizationMemberUseCase(
    ILoggerFactory loggerFactory,
    IValidator<AddOrganizationMemberCommand> validator,
    IOrganizationRepository organizations,
    IOrganizationMemberRepository members,
    IPlatformUserRepository users)
    : CommandUseCase<AddOrganizationMemberCommand, OrganizationMemberDto>(loggerFactory, validator)
{
    protected override async Task<ErrorOr<OrganizationMemberDto>> ExecuteCore(
        AddOrganizationMemberCommand request,
        CancellationToken ct)
    {
        if (await organizations.GetByIdAsync(request.OrganizationId, ct) is null)
            return OrganizationErrors.NotFound(request.OrganizationId);

        var email = request.Email.Trim().ToLowerInvariant();

        var user = await users.GetByEmailAsync(email, ct);
        if (user is null)
            return OrganizationErrors.MemberUserNotFound(email);

        if (await members.GetAsync(request.OrganizationId, user.Id, ct) is not null)
            return OrganizationErrors.MemberAlreadyExists(email);

        var role = OrganizationRole.FromName(request.Role.Trim());
        var member = OrganizationMember.Create(request.OrganizationId, user.Id, role);

        await members.AddAsync(member, ct);

        return new OrganizationMemberDto(user.Id, user.Email, role.Name, member.CreatedAt);
    }
}
