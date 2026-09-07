using NovaFE.Application.Webhooks.Contracts;

namespace NovaFE.Application.Webhooks.Interfaces;

/// <summary>
/// Outbox de entrega de webhooks sobre <c>webhook_deliveries</c>. Mismo patrón que
/// <c>IEcfSubmissionQueue</c>: <c>FOR UPDATE SKIP LOCKED</c> + <c>locked_by</c> por
/// llamada. El <c>Enqueue</c> hace <em>fan-out</em>: una fila por endpoint del
/// tenant, habilitado y suscrito al tipo.
/// </summary>
public interface IWebhookOutbox
{
    /// <summary>
    /// Encola una entrega por cada endpoint suscrito. Devuelve cuántas filas se
    /// crearon (0 si el tenant no tiene endpoints para ese evento — no-op).
    /// </summary>
    Task<int> EnqueueAsync(WebhookEventEnvelope envelope, Guid tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookDeliveryItem>> ClaimBatchAsync(int max, CancellationToken ct = default);

    Task CompleteAsync(Guid rowId, int statusCode, CancellationToken ct = default);

    Task RescheduleAsync(
        Guid rowId, DateTimeOffset nextAttemptAt, int attempts, int? statusCode, string? lastError, CancellationToken ct = default);

    Task MarkDeadAsync(Guid rowId, int? statusCode, string? lastError, CancellationToken ct = default);

    /// <summary>Recupera filas atascadas en <c>processing</c> (worker caído a mitad de una entrega).</summary>
    Task<int> ReapStuckAsync(TimeSpan olderThan, CancellationToken ct = default);

    /// <summary>Borra el log de entregas (<c>delivered</c> / <c>dead</c>) más viejo que <paramref name="olderThan"/>.</summary>
    Task<int> PurgeAsync(TimeSpan olderThan, CancellationToken ct = default);
}

/// <summary>Una fila de entrega reclamada, lista para procesar.</summary>
public sealed record WebhookDeliveryItem(
    Guid RowId,
    Guid TenantId,
    Guid EndpointId,
    string EventId,
    string EventType,
    string Payload,
    int Attempts);
