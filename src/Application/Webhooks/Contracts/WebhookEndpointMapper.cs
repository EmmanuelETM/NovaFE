using NovaFE.Domain.Webhooks;

namespace NovaFE.Application.Webhooks.Contracts;

/// <summary>Proyección del agregado a la vista pública. Para las lecturas puras se usa Dapper directo.</summary>
internal static class WebhookEndpointMapper
{
    public static WebhookEndpointDto ToDto(WebhookEndpoint endpoint) => new(
        endpoint.Id,
        endpoint.TenantId,
        endpoint.Url,
        endpoint.Description,
        endpoint.Events,
        endpoint.Enabled,
        endpoint.ConsecutiveFailures,
        endpoint.DisabledReason,
        endpoint.DisabledAt,
        endpoint.CreatedAt,
        endpoint.UpdatedAt);
}
