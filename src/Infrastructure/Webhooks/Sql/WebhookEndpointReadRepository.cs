using Dapper;
using NovaFE.Application.Webhooks.Contracts;
using NovaFE.Application.Webhooks.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Webhooks.Sql;

/// <summary>
/// Lectura de los endpoints de webhook con Dapper. Nunca proyecta el
/// <c>secret</c>. Sin interceptores: el <c>WHERE is_deleted</c> y el
/// <c>tenant_id</c> van explícitos. <c>events</c> es <c>text[]</c> → <c>string[]</c>.
/// </summary>
internal sealed class WebhookEndpointReadRepository(IDbSession session) : IWebhookEndpointReadRepository
{
    private const string Columns =
        """
        id                   AS "Id",
        tenant_id            AS "TenantId",
        url                  AS "Url",
        description          AS "Description",
        events               AS "Events",
        enabled              AS "Enabled",
        consecutive_failures AS "ConsecutiveFailures",
        disabled_reason      AS "DisabledReason",
        disabled_at          AS "DisabledAt",
        created_at           AS "CreatedAt",
        updated_at           AS "UpdatedAt"
        """;

    public async Task<IReadOnlyList<WebhookEndpointDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {Columns}
            FROM webhook_endpoints
            WHERE tenant_id = @tenantId AND is_deleted = false
            ORDER BY created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<WebhookEndpointDto>(
            new CommandDefinition(sql, new { tenantId }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }

    public async Task<WebhookEndpointDto?> GetAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {Columns}
            FROM webhook_endpoints
            WHERE id = @id AND tenant_id = @tenantId AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<WebhookEndpointDto>(
            new CommandDefinition(sql, new { id, tenantId }, session.Transaction, cancellationToken: ct));
    }
}
