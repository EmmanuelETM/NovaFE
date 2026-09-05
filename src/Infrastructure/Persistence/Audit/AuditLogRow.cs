namespace NovaFE.Infrastructure.Persistence.Audit;

/// <summary>
/// Fila de <c>audit_log</c> (RF-14.4). Solo define el esquema; la escritura la
/// hace <see cref="AuditLogWriter"/> con Dapper (sin update/delete — esa
/// ausencia es la inmutabilidad). Tabla de sistema: <b>no</b> es
/// <c>ITenantOwned</c> ni lleva RLS — debe poder registrar acciones de operador y
/// peticiones anónimas rechazadas, que no tienen tenant.
/// </summary>
internal sealed class AuditLogRow
{
    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Guid? TenantId { get; private set; }

    /// <summary><c>apikey:{id}</c> / <c>operator</c> / <c>anonymous</c>.</summary>
    public string Actor { get; private set; } = null!;

    /// <summary><c>admin_tenant</c> / <c>emisor</c> / <c>consultor</c> / <c>admin_sistema</c>; null si no autenticó.</summary>
    public string? ActorRole { get; private set; }

    public string? IpAddress { get; private set; }

    public string HttpMethod { get; private set; } = null!;

    public string Path { get; private set; } = null!;

    public int StatusCode { get; private set; }

    public bool Succeeded { get; private set; }

    public string? TraceId { get; private set; }

    public int? DurationMs { get; private set; }
}
