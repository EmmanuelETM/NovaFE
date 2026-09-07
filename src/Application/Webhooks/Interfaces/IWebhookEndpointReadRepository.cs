using NovaFE.Application.Webhooks.Contracts;

namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>Read side (Dapper) de los endpoints de webhook.</summary>
public interface IWebhookEndpointReadRepository
{
    /// <summary>Los endpoints de un contribuyente, el más reciente primero. Sin secret.</summary>
    Task<IReadOnlyList<WebhookEndpointDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Un endpoint del contribuyente, o <c>null</c>. Sin secret.</summary>
    Task<WebhookEndpointDto?> GetAsync(Guid id, Guid tenantId, CancellationToken ct = default);
}
