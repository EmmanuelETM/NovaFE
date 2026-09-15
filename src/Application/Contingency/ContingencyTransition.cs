namespace NovaFE.Application.Contingency;

/// <summary>Cambio de estado de <c>platform.contingency_mode</c> recién aplicado.</summary>
/// <param name="EventType"><see cref="NovaFE.Domain.Webhooks.WebhookEventType.ContingencyActivated"/> o su contraparte.</param>
public sealed record ContingencyTransition(string EventType, ContingencyStatusPayload Payload);

/// <summary>
/// Payload del webhook <c>contingency.activated</c>/<c>contingency.deactivated</c> —
/// no hay un recurso único de por medio (es un cambio de estado de plataforma, no
/// de un e-CF puntual), así que no reusa ningún DTO existente.
/// </summary>
/// <param name="Status"><c>"active"</c> o <c>"inactive"</c>.</param>
/// <param name="OldestPendingMinutes">
/// Antigüedad, en minutos, de la fila más vieja del outbox de envío al momento de
/// la transición — la señal que la disparó (o que ya no la sostiene).
/// </param>
public sealed record ContingencyStatusPayload(string Status, DateTimeOffset ChangedAt, double? OldestPendingMinutes);
