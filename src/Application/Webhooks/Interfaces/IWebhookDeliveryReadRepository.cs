using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Domain.Common;

namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>Read side (Dapper) del log de entregas de webhook.</summary>
public interface IWebhookDeliveryReadRepository
{
    Task<PagedResult<WebhookDeliveryDto>> ListByEndpointAsync(
        Guid endpointId, Guid tenantId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Entregas <c>dead</c> de toda la plataforma, para el panel de operador.</summary>
    Task<PagedResult<DeadWebhookDeliveryDto>> ListDeadAcrossTenantsAsync(
        int page, int pageSize, CancellationToken ct = default);
}
