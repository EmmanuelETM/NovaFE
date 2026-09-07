namespace NovaFE.Infrastructure.Persistence.Notifications;

/// <summary>
/// Fila de <c>expiry_notifications</c> — un aviso de vencimiento ya emitido. Tabla
/// de sistema: <b>no</b> lleva RLS. Solo define el esquema; la lógica de reclamo
/// idempotente la hace <c>PostgresExpiryNotificationLog</c>.
/// </summary>
internal sealed class ExpiryNotificationRow
{
    public Guid Id { get; private set; }

    /// <summary><c>certificate</c> | <c>sequence</c>.</summary>
    public string SubjectType { get; private set; } = null!;

    public Guid SubjectId { get; private set; }

    /// <summary>El aviso concreto: <c>expiring:30</c>, <c>expired</c>, <c>low</c>, <c>exhausted</c>…</summary>
    public string Kind { get; private set; } = null!;

    public DateTimeOffset NotifiedAt { get; private set; }
}
