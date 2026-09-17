using NovaFE.Domain.Organizations;

namespace NovaFE.Application.Organizations.Interfaces;

/// <summary>Write side (EF Core) de las membresías de organización.</summary>
public interface IOrganizationMemberRepository
{
    Task<OrganizationMember?> GetAsync(Guid organizationId, Guid platformUserId, CancellationToken ct = default);

    Task AddAsync(OrganizationMember member, CancellationToken ct = default);

    Task UpdateAsync(OrganizationMember member, CancellationToken ct = default);

    Task RemoveAsync(OrganizationMember member, CancellationToken ct = default);
}
