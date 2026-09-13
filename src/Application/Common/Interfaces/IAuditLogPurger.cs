namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// Purga filas viejas de <c>audit_log</c> por retención. Deliberadamente
/// separada de <see cref="IAuditLogWriter"/>: ese contrato es insert-only a
/// propósito (esa ausencia de update/delete es la garantía de inmutabilidad),
/// así que el borrado por antigüedad vive en su propio contrato, para un
/// trabajo de sistema explícito — no en el camino que sirve requests.
/// </summary>
public interface IAuditLogPurger
{
    /// <summary>Borra filas con <c>occurred_at</c> más viejo que <c>olderThan</c>. Devuelve cuántas.</summary>
    Task<int> PurgeAsync(TimeSpan olderThan, CancellationToken ct = default);
}
