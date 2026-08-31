namespace NovaFE.Application.Common.Interfaces;

/// <summary>Qué hacer con una petición que trae una <c>Idempotency-Key</c>.</summary>
public enum IdempotencyDecision
{
    /// <summary>Clave nueva: se reservó la fila; el caso de uso puede continuar.</summary>
    Proceed,

    /// <summary>Otra petición con la misma clave está en curso ahora mismo.</summary>
    InProgress,

    /// <summary>La clave ya se usó con un cuerpo distinto.</summary>
    Conflict,

    /// <summary>La clave ya se completó con este mismo cuerpo: devolver el resultado original.</summary>
    Replay,
}

/// <param name="Decision">Qué hacer.</param>
/// <param name="EcfId">El comprobante original, solo cuando <see cref="IdempotencyDecision.Replay"/>.</param>
public sealed record IdempotencyOutcome(IdempotencyDecision Decision, Guid? EcfId = null);

/// <summary>
/// Almacén durable de claves de idempotencia (PostgreSQL, no caché — exige unicidad
/// y durabilidad). Una clave se <see cref="BeginAsync"/> al empezar y se
/// <see cref="CompleteAsync"/> al terminar con éxito, en la misma transacción que
/// el recurso creado.
/// </summary>
public interface IIdempotencyStore
{
    Task<IdempotencyOutcome> BeginAsync(
        Guid tenantId, string key, string requestHash, CancellationToken ct = default);

    Task CompleteAsync(
        Guid tenantId, string key, Guid resourceId, CancellationToken ct = default);
}
