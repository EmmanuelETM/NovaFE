namespace NovaFE.Application.Webhooks.DeleteEndpoint;

/// <summary>Elimina un endpoint (borrado lógico). Deja de recibir entregas de inmediato.</summary>
public sealed record DeleteWebhookEndpointCommand(Guid Id);
