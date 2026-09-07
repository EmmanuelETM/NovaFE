namespace NovaFE.Application.Notifications;

/// <summary>
/// Registro de qué avisos de vencimiento ya se emitieron, para no repetirlos en
/// cada pasada del monitor. La clave es <c>(subjectType, subjectId, kind)</c>;
/// <paramref name="kind"/> distingue el umbral concreto (p. ej.
/// <c>expiring:30</c>).
/// </summary>
public interface IExpiryNotificationLog
{
    /// <summary>
    /// Marca el aviso como emitido. Devuelve <c>true</c> si es nuevo (hay que
    /// enviarlo), <c>false</c> si ya estaba registrado. Atómico ante concurrencia.
    /// </summary>
    Task<bool> TryRecordAsync(string subjectType, Guid subjectId, string kind, CancellationToken ct = default);
}
