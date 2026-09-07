using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Webhooks;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore;

namespace NovaFE.Infrastructure.Webhooks.EfCore;

internal sealed class WebhookEndpointRepository(AppDbContext context) : IWebhookEndpointRepository
{
    // El filtro global "Tenant" de EF acota estas consultas al contribuyente actual.
    public Task<WebhookEndpoint?> GetAsync(Guid id, CancellationToken ct = default)
        => context.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<int> CountAsync(CancellationToken ct = default)
        => context.WebhookEndpoints.CountAsync(ct);

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        await context.WebhookEndpoints.AddAsync(endpoint, ct);
        await context.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        // El SoftDeleteInterceptor traduce el Remove a is_deleted = true.
        context.WebhookEndpoints.Remove(endpoint);
        return context.SaveChangesAsync(ct);
    }
}
