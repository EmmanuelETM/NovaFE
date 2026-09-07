namespace NovaFE.Application.Webhooks.CreateEndpoint;

/// <summary>
/// Registra un endpoint de webhook para el contribuyente de la petición. El
/// tenant sale de <c>ICurrentTenant</c>, no del cuerpo.
/// </summary>
public sealed record CreateWebhookEndpointCommand(
    string? Url,
    IReadOnlyList<string>? Events,
    string? Description);
