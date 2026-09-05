namespace NovaFE.Application.Audit.Contracts;

/// <summary>Una fila del registro de auditoría (RF-14.4), tal como se lista.</summary>
public sealed record AuditLogEntryDto(
    Guid Id,
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
