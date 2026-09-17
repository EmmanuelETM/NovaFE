using Dapper;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Webhooks.Sql;

/// <summary>Log de entregas paginado con Dapper. Sin el <c>payload</c>.</summary>
internal sealed class WebhookDeliveryReadRepository(IDbSession session) : IWebhookDeliveryReadRepository
{
    public async Task<PagedResult<WebhookDeliveryDto>> ListByEndpointAsync(
        Guid endpointId, Guid tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        const string countSql =
            "SELECT count(*) FROM webhook_deliveries WHERE endpoint_id = @endpointId AND tenant_id = @tenantId";
        const string pageSql =
            """
            SELECT id               AS "Id",
                   event_id         AS "EventId",
                   event_type       AS "EventType",
                   status           AS "Status",
                   attempts         AS "Attempts",
                   last_status_code AS "LastStatusCode",
                   last_error       AS "LastError",
                   next_attempt_at  AS "NextAttemptAt",
                   created_at       AS "CreatedAt",
                   updated_at       AS "UpdatedAt"
            FROM webhook_deliveries
            WHERE endpoint_id = @endpointId AND tenant_id = @tenantId
            ORDER BY created_at DESC
            LIMIT @take OFFSET @skip
            """;

        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var connection = await session.GetConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, new { endpointId, tenantId }, session.Transaction, cancellationToken: ct));

        var items = await connection.QueryAsync<WebhookDeliveryDto>(
            new CommandDefinition(
                pageSql, new { endpointId, tenantId, take = pageSize, skip }, session.Transaction, cancellationToken: ct));

        return new PagedResult<WebhookDeliveryDto>([.. items], total, page, pageSize);
    }

    public async Task<PagedResult<DeadWebhookDeliveryDto>> ListDeadAcrossTenantsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        const string countSql = "SELECT count(*) FROM webhook_deliveries WHERE status = 'dead'";
        const string pageSql =
            """
            SELECT d.tenant_id       AS "TenantId",
                   t.rnc             AS "Rnc",
                   t.legal_name      AS "LegalName",
                   d.endpoint_id     AS "EndpointId",
                   e.url             AS "Url",
                   d.id              AS "DeliveryId",
                   d.event_type      AS "EventType",
                   d.attempts        AS "Attempts",
                   d.last_status_code AS "LastStatusCode",
                   d.updated_at      AS "UpdatedAt"
            FROM webhook_deliveries d
            JOIN tenants t ON t.id = d.tenant_id
            JOIN webhook_endpoints e ON e.id = d.endpoint_id
            WHERE d.status = 'dead'
            ORDER BY d.updated_at DESC
            LIMIT @take OFFSET @skip
            """;

        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var connection = await session.GetConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, transaction: session.Transaction, cancellationToken: ct));

        var items = await connection.QueryAsync<DeadWebhookDeliveryDto>(
            new CommandDefinition(pageSql, new { take = pageSize, skip }, session.Transaction, cancellationToken: ct));

        return new PagedResult<DeadWebhookDeliveryDto>([.. items], total, page, pageSize);
    }
}
