namespace NovaFE.Application.Webhooks.RetryDelivery;

/// <summary>
/// Reintenta manualmente una entrega <c>dead</c> — la explícitamente fuera de
/// alcance de v1 en <c>docs/webhooks.md</c>, ahora resuelta. El tenant va
/// aparte porque <c>webhook_deliveries</c> es tabla de sistema sin RLS.
/// </summary>
public sealed record RetryWebhookDeliveryCommand(Guid TenantId, Guid DeliveryId);
