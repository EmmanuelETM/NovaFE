using Dapper;
using NovaFE.Application.Ops.Contracts;
using NovaFE.Application.Ops.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Ops.Sql;

/// <summary>
/// Lectura cross-tenant para el panel de operación. <c>ecf_submission_outbox</c>
/// y <c>webhook_deliveries</c> son tablas de sistema sin RLS (ver
/// <c>docs/dgii-submission.md</c> / <c>docs/webhooks.md</c>), así que la
/// consulta cross-tenant es segura ahí. <c>ncf_sequences</c> sí tiene RLS —
/// esta consulta asume una conexión sin <c>app.tenant_id</c> fijado que aún
/// pueda ver todas las filas, cierto hoy porque el runtime todavía no corre
/// como el rol restringido <c>novafe_app</c> (ver <c>docs/roadmap.md</c>,
/// "Prueba de aislamiento RLS"). Cuando ese rol entre en producción, esta
/// consulta necesitará su propio camino (una política de RLS para un rol de
/// operador, o una conexión aparte) — documentado, no resuelto acá.
/// </summary>
internal sealed class OpsStatusReadRepository(IDbSession session) : IOpsStatusReadRepository
{
    public Task<OutboxStatusDto> GetEcfSubmissionOutboxStatusAsync(CancellationToken ct = default)
        => GetOutboxStatusAsync("ecf_submission_outbox", ct);

    public Task<OutboxStatusDto> GetWebhookOutboxStatusAsync(CancellationToken ct = default)
        => GetOutboxStatusAsync("webhook_deliveries", ct);

    // Tope duro: esto es para que un operador vea a quién avisar, no un
    // reporte completo — si algún día hay más de 20 tenants bajos de stock a
    // la vez, ese ya es un problema de otro tamaño.
    private const int MaxSequencesAtRisk = 20;

    public async Task<IReadOnlyList<SequenceAtRiskDto>> GetSequencesAtRiskAsync(
        decimal lowStockFraction, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT
                t.legal_name                               AS "TenantName",
                ('E' || s.ecf_type::text)                   AS "Type",
                greatest(s.range_to - s."next" + 1, 0)       AS "Remaining"
            FROM ncf_sequences s
            JOIN tenants t ON t.id = s.tenant_id
            WHERE s.active AND s.is_deleted = false
                AND greatest(s.range_to - s."next" + 1, 0)
                    <= ceiling((s.range_to - s.range_from + 1) * @lowStockFraction)
            ORDER BY "Remaining" ASC
            LIMIT @limit
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<SequenceAtRiskDto>(
            new CommandDefinition(
                sql, new { lowStockFraction, limit = MaxSequencesAtRisk }, session.Transaction, cancellationToken: ct));

        return rows.AsList();
    }

    // El nombre de tabla es un literal fijo del propio código, nunca entra del
    // cliente — interpolarlo acá no es una inyección SQL.
    private async Task<OutboxStatusDto> GetOutboxStatusAsync(string table, CancellationToken ct)
    {
        var sql =
            $"""
            SELECT
                count(*) FILTER (WHERE status = 'pending')::int                   AS "Pending",
                count(*) FILTER (WHERE status = 'processing')::int                AS "Processing",
                count(*) FILTER (WHERE status = 'dead')::int                      AS "Dead",
                min(created_at) FILTER (WHERE status IN ('pending', 'processing')) AS "OldestPendingAt"
            FROM {table}
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleAsync<OutboxStatusDto>(
            new CommandDefinition(sql, transaction: session.Transaction, cancellationToken: ct));
    }
}
