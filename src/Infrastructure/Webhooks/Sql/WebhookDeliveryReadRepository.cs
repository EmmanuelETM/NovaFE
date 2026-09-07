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
}
