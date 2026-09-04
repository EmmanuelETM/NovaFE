using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Tenants;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Tenants.EfCore;

internal sealed class ApiKeyRepository(AppDbContext context) : IApiKeyRepository
{
    public Task<ApiKey?> GetAsync(Guid id, Guid tenantId, CancellationToken ct = default)
        => context.ApiKeys.FirstOrDefaultAsync(k => k.Id == id && k.TenantId == tenantId, ct);

    public async Task AddAsync(ApiKey key, CancellationToken ct = default)
    {
        await context.ApiKeys.AddAsync(key, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(ApiKey key, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public Task TouchAsync(Guid id, DateTimeOffset usedAt, CancellationToken ct = default)
        => context.ApiKeys
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, usedAt), ct);
}
