namespace NovaFE.Application.Common.Interfaces;

/// <summary>
/// Escribe filas del registro de auditoría inmutable (RF-14.4). Sin
/// update/delete — esa ausencia es la garantía de inmutabilidad a nivel de
/// aplicación, igual que el outbox de envío no tiene borrado.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(AuditLogEntry entry, CancellationToken ct = default);
}
