namespace NovaFE.Application.Webhooks.PingEndpoint;

/// <summary>Envía un evento <c>webhook.ping</c> a un endpoint y espera el resultado de la entrega.</summary>
public sealed record PingWebhookEndpointCommand(Guid Id);
