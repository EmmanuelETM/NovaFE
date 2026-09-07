using NovaFE.Domain.Webhooks;

namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>
/// Write side (EF Core) de los <see cref="WebhookEndpoint"/>. Las consultas se
/// acotan al tenant actual por el filtro global de EF (igual que
/// <c>ICertificateRepository</c>).
/// </summary>
public interface IWebhookEndpointRepository
{
    Task<WebhookEndpoint?> GetAsync(Guid id, CancellationToken ct = default);

    Task<int> CountAsync(CancellationToken ct = default);

    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);

    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default);

    /// <summary>Borrado lógico (lo aplica el interceptor de EF).</summary>
    Task RemoveAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
}
