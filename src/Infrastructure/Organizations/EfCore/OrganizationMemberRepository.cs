using Microsoft.EntityFrameworkCore;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Domain.Organizations;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Organizations.EfCore;

internal sealed class OrganizationMemberRepository(AppDbContext context) : IOrganizationMemberRepository
{
    public Task<OrganizationMember?> GetAsync(Guid organizationId, Guid platformUserId, CancellationToken ct = default)
        => context.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.PlatformUserId == platformUserId, ct);

    public async Task AddAsync(OrganizationMember member, CancellationToken ct = default)
    {
        await context.OrganizationMembers.AddAsync(member, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(OrganizationMember member, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public Task RemoveAsync(OrganizationMember member, CancellationToken ct = default)
    {
        context.OrganizationMembers.Remove(member);
        return context.SaveChangesAsync(ct);
    }
}
