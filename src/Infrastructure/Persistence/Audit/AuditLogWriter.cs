using Dapper;
using NovaFE.Application.Common;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Persistence.Audit;

/// <summary>
/// Escribe en <c>audit_log</c> con un <c>INSERT</c> crudo (mismo patrón que
/// <c>PostgresIdempotencyStore</c>) — no hay <c>UPDATE</c>/<c>DELETE</c> en esta
/// clase, que es la garantía de inmutabilidad a nivel de aplicación.
/// </summary>
internal sealed class AuditLogWriter(IDbSession session) : IAuditLogWriter
{
    public async Task WriteAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        var connection = await session.GetConnectionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO audit_log
                (id, occurred_at, tenant_id, actor, actor_role, ip_address,
                 http_method, path, status_code, succeeded, trace_id, duration_ms)
            VALUES
                (@id, @occurredAt, @tenantId, @actor, @actorRole, @ipAddress,
                 @httpMethod, @path, @statusCode, @succeeded, @traceId, @durationMs)
            """,
            new
            {
                id = Guid.CreateVersion7(),
                occurredAt = entry.OccurredAt,
                tenantId = entry.TenantId,
                actor = entry.Actor,
                actorRole = entry.ActorRole,
                ipAddress = entry.IpAddress,
                httpMethod = entry.HttpMethod,
                path = entry.Path,
                statusCode = entry.StatusCode,
                succeeded = entry.Succeeded,
                traceId = entry.TraceId,
                durationMs = entry.DurationMs,
            },
            session.Transaction, cancellationToken: ct));
    }
}
