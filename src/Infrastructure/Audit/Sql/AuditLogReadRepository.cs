using Dapper;
using NovaFE.Application.Audit.Contracts;
using NovaFE.Application.Audit.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Audit.Sql;

/// <summary>Lectura paginada de <c>audit_log</c> con Dapper.</summary>
internal sealed class AuditLogReadRepository(IDbSession session) : IAuditLogReadRepository
{
    public async Task<PagedResult<AuditLogEntryDto>> ListByTenantAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        const string countSql = "SELECT count(*) FROM audit_log WHERE tenant_id = @tenantId";
        const string pageSql =
            """
            SELECT id           AS "Id",
                   occurred_at  AS "OccurredAt",
                   tenant_id    AS "TenantId",
                   actor        AS "Actor",
                   actor_role   AS "ActorRole",
                   ip_address   AS "IpAddress",
                   http_method  AS "HttpMethod",
                   path         AS "Path",
                   status_code  AS "StatusCode",
                   succeeded    AS "Succeeded",
                   trace_id     AS "TraceId",
                   duration_ms  AS "DurationMs"
            FROM audit_log
            WHERE tenant_id = @tenantId
            ORDER BY occurred_at DESC
            LIMIT @take OFFSET @skip
            """;

        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var parameters = new { tenantId, take = pageSize, skip };

        var connection = await session.GetConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { tenantId }, session.Transaction, cancellationToken: ct));

        var items = await connection.QueryAsync<AuditLogEntryDto>(
            new CommandDefinition(pageSql, parameters, session.Transaction, cancellationToken: ct));

        return new PagedResult<AuditLogEntryDto>([.. items], total, page, pageSize);
    }
}
