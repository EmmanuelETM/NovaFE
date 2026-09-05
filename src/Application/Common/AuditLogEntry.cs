namespace NovaFE.Application.Common;

/// <summary>
/// Una fila del registro de auditoría (RF-14.4): quién hizo qué, cuándo y con qué
/// resultado. Sin cuerpo de request/response — no hace falta para el requisito y
/// evita capturar datos fiscales o PII de más.
/// </summary>
public sealed record AuditLogEntry(
    DateTimeOffset OccurredAt,
    Guid? TenantId,
    string Actor,
    string? ActorRole,
    string? IpAddress,
    string HttpMethod,
    string Path,
    int StatusCode,
    bool Succeeded,
    string? TraceId,
    int? DurationMs);
