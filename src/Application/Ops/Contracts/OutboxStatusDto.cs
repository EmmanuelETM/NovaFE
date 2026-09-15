namespace NovaFE.Application.Ops.Contracts;

/// <summary>
/// Profundidad de un outbox (<c>ecf_submission_outbox</c> o
/// <c>webhook_deliveries</c>) por estado. <see cref="OldestPendingAt"/> es la
/// fecha de creación de la fila <c>pending</c>/<c>processing</c> más vieja —
/// mejor señal que la profundidad sola: dice si está estancado, no solo cuánto hay.
/// </summary>
public sealed record OutboxStatusDto(
    int Pending,
    int Processing,
    int Dead,
    DateTimeOffset? OldestPendingAt);
