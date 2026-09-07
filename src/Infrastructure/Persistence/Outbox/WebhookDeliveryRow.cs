namespace NovaFE.Infrastructure.Persistence.Outbox;

/// <summary>
/// Fila de <c>webhook_deliveries</c> — una entrega pendiente (o su registro
/// histórico). Tabla de sistema: <b>no</b> es <c>ITenantOwned</c> ni lleva RLS
/// (lleva <see cref="TenantId"/> solo para que el worker reconstruya el contexto
/// del tenant al procesarla). La lógica de reclamo la hace <c>PostgresWebhookOutbox</c>.
/// </summary>
internal sealed class WebhookDeliveryRow
{
    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid EndpointId { get; private set; }

    /// <summary>Id del evento (<c>evt_…</c>), compartido por todo el fan-out.</summary>
    public string EventId { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    /// <summary>El JSON del sobre, tal cual se entrega y se firma.</summary>
    public string Payload { get; private set; } = null!;

    /// <summary><c>pending</c> | <c>processing</c> | <c>delivered</c> | <c>dead</c>.</summary>
    public string Status { get; private set; } = null!;

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public int? LastStatusCode { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    public Guid? LockedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
