namespace NovaFE.Application.Webhooks.Contracts;

/// <summary>Una entrega (o intento) registrada para un endpoint. Log, no el payload completo.</summary>
public sealed record WebhookDeliveryDto(
    Guid Id,
    string EventId,
    string EventType,
    string Status,
    int Attempts,
    int? LastStatusCode,
    string? LastError,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
