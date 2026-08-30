using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Persistence.EfCore.Repositories;

internal sealed class TenantRepository(AppDbContext context) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => context.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> RncExistsAsync(string rnc, CancellationToken ct = default)
    {
        var value = Rnc.FromStorage(rnc);
        return context.Tenants.AnyAsync(t => t.Rnc == value, ct);
    }

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        await context.Tenants.AddAsync(tenant, ct);
        await context.SaveChangesAsync(ct);
    }
}
