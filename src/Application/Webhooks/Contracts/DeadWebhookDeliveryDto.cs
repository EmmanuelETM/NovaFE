namespace NovaFE.Application.Webhooks.Contracts;

/// <summary>
/// Una entrega <c>dead</c>, vista cross-tenant para el operador (<c>GET /ops/dead-deliveries</c>).
/// Trae la identidad del contribuyente porque, a diferencia de <see cref="WebhookDeliveryDto"/>,
/// no hay un tenant activo que la dé por contexto.
/// </summary>
public sealed record DeadWebhookDeliveryDto(
    Guid TenantId,
    string Rnc,
    string LegalName,
    Guid EndpointId,
    string Url,
    Guid DeliveryId,
    string EventType,
    int Attempts,
    int? LastStatusCode,
    DateTimeOffset UpdatedAt);
