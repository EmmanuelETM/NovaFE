using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Tenants.EfCore;

internal sealed class EmitterProfileRepository(AppDbContext context) : IEmitterProfileRepository
{
    public Task<EmitterProfile?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => context.EmitterProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);

    public async Task AddAsync(EmitterProfile profile, CancellationToken ct = default)
    {
        await context.EmitterProfiles.AddAsync(profile, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(EmitterProfile profile, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);
}
