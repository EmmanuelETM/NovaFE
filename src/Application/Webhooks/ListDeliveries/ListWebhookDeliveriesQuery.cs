namespace NovaFE.Application.Webhooks.ListDeliveries;

/// <summary>Log de entregas de un endpoint, paginado (más reciente primero).</summary>
public sealed record ListWebhookDeliveriesQuery(Guid EndpointId, int Page = 1, int PageSize = 20);
