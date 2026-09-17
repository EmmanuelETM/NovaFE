using Microsoft.EntityFrameworkCore;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.Infrastructure.Persistence.EfCore;

namespace NovaFE.Infrastructure.Tenants.EfCore;

internal sealed class TenantMemberRepository(AppDbContext context) : ITenantMemberRepository
{
    public Task<TenantMember?> GetAsync(Guid tenantId, Guid platformUserId, CancellationToken ct = default)
        => context.TenantMembers
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.PlatformUserId == platformUserId, ct);

    public async Task AddAsync(TenantMember member, CancellationToken ct = default)
    {
        await context.TenantMembers.AddAsync(member, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(TenantMember member, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
