namespace NovaFE.Application.Webhooks.RotateEndpointSecret;

/// <summary>Genera un secret nuevo para el endpoint. El viejo deja de valer al instante.</summary>
public sealed record RotateWebhookEndpointSecretCommand(Guid Id);
