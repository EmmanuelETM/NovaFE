namespace NovaFE.Infrastructure.Persistence.Outbox;

/// <summary>
/// Fila de <c>ecf_submission_outbox</c> — la cola de envío de e-CF a la DGII. Solo
/// define el esquema; la lógica de reclamo/reprogramación la hace
/// <c>PostgresEcfSubmissionQueue</c>. Tabla de sistema: <b>no</b> es
/// <c>ITenantOwned</c> ni lleva RLS (lleva <see cref="TenantId"/> solo para que el
/// worker reconstruya el contexto del tenant al procesarla).
/// </summary>
internal sealed class EcfSubmissionOutboxRow
{
    public Guid Id { get; private set; }

    public Guid EcfId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary><c>DgiiEnvironment.Name</c>.</summary>
    public string Environment { get; private set; } = null!;

    /// <summary><c>submit</c> | <c>poll</c>.</summary>
    public string Kind { get; private set; } = null!;

    /// <summary><c>pending</c> | <c>processing</c> | <c>done</c> | <c>dead</c>.</summary>
    public string Status { get; private set; } = null!;

    public int Attempts { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public string? TrackId { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? LockedAt { get; private set; }

    /// <summary>Token único por llamada de reclamo, para leer de vuelta lo que se acaba de reclamar.</summary>
    public Guid? LockedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
}
