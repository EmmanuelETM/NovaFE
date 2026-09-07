namespace NovaFE.Application.Webhooks.Contracts;

/// <summary>Vista de un endpoint de webhook. Nunca incluye el <c>secret</c>.</summary>
public sealed record WebhookEndpointDto(
    Guid Id,
    Guid TenantId,
    string Url,
    string? Description,
    string[] Events,
    bool Enabled,
    int ConsecutiveFailures,
    string? DisabledReason,
    DateTimeOffset? DisabledAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// Respuesta de la creación de un endpoint: la vista más el <b>secret en claro</b>,
/// que solo se devuelve aquí (y en <c>rotate-secret</c>) y no se puede recuperar.
/// </summary>
public sealed record WebhookEndpointCreatedDto(WebhookEndpointDto Endpoint, string Secret);

/// <summary>Respuesta de <c>rotate-secret</c>: el nuevo secret en claro.</summary>
public sealed record WebhookSecretDto(string Secret);

/// <summary>Resultado de un <c>ping</c>: si el endpoint respondió <c>2xx</c> y con qué código.</summary>
public sealed record WebhookPingResultDto(bool Delivered, int? StatusCode, string? Error);
