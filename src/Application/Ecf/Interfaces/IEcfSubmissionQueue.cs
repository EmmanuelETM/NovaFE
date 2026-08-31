using NovaFE.Domain.Common;

namespace NovaFE.Application.Ecf.Interfaces;

/// <summary>Qué paso del envío representa una fila de la cola.</summary>
public enum EcfSubmissionKind
{
    /// <summary>Enviar el comprobante a la DGII y obtener el <c>TrackId</c>.</summary>
    Submit,

    /// <summary>Consultar el resultado por <c>TrackId</c>.</summary>
    Poll,
}

/// <summary>Una fila de trabajo reclamada de la cola de envío.</summary>
public sealed record EcfSubmissionWorkItem(
    Guid Id,
    Guid EcfId,
    Guid TenantId,
    DgiiEnvironment Environment,
    EcfSubmissionKind Kind,
    int Attempts,
    string? TrackId);

/// <summary>
/// Cola de envío de e-CF a la DGII (outbox sobre PostgreSQL, <c>FOR UPDATE SKIP
/// LOCKED</c>). El <c>EnqueueSubmitAsync</c> se llama dentro de la misma
/// transacción que persiste el comprobante; el resto lo consume el worker (y el
/// fast-path inline).
/// </summary>
public interface IEcfSubmissionQueue
{
    /// <summary>Encola el primer envío de un comprobante recién persistido.</summary>
    Task EnqueueSubmitAsync(Guid ecfId, Guid tenantId, DgiiEnvironment environment, CancellationToken ct = default);

    /// <summary>Reclama hasta <paramref name="max"/> filas vencidas y las marca <c>processing</c>.</summary>
    Task<IReadOnlyList<EcfSubmissionWorkItem>> ClaimBatchAsync(int max, CancellationToken ct = default);

    /// <summary>Reclama la fila pendiente de un comprobante concreto (fast-path inline).</summary>
    Task<EcfSubmissionWorkItem?> ClaimByEcfAsync(Guid ecfId, CancellationToken ct = default);

    /// <summary>Marca una fila como terminada.</summary>
    Task CompleteAsync(Guid rowId, CancellationToken ct = default);

    /// <summary>Reprograma una fila para el siguiente intento.</summary>
    Task RescheduleAsync(
        Guid rowId,
        EcfSubmissionKind kind,
        DateTimeOffset nextAttemptAt,
        int attempts,
        string? trackId = null,
        string? lastError = null,
        CancellationToken ct = default);

    /// <summary>Marca una fila como muerta (no se reintenta más).</summary>
    Task MarkDeadAsync(Guid rowId, string lastError, CancellationToken ct = default);

    /// <summary>Devuelve a <c>pending</c> las filas atascadas en <c>processing</c>.</summary>
    Task<int> ReapStuckAsync(TimeSpan olderThan, CancellationToken ct = default);
}
