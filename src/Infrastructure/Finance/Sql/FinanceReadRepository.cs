using Dapper;
using NovaFE.Application.Finance.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Finance.Sql;

/// <summary>
/// Lectura del resumen fiscal con Dapper. <c>totals</c> es un único <c>jsonb</c>
/// (ver <c>EcfConfiguration.cs</c>) — se extrae con <c>-&gt;&gt;</c> en vez de sumar
/// columnas propias; sin interceptores, así que <c>is_deleted</c> va explícito.
/// Un e-CF <c>rejected</c> se excluye de los dos métodos: nunca quedó
/// facturado ante la DGII.
/// </summary>
internal sealed class FinanceReadRepository(IDbSession session) : IFinanceReadRepository
{
    private const string Where =
        """
        WHERE tenant_id = @tenantId
          AND is_deleted = false
          AND status != 'rejected'
          AND issue_date BETWEEN @from AND @to
        """;

    public async Task<FiscalTotalsLookup> GetTotalsAsync(
        Guid tenantId, DateOnly from, DateOnly through, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT
                count(*)::int                                                  AS "TotalCount",
                coalesce(sum(monto_total), 0)                                  AS "TotalInvoiced",
                coalesce(sum((totals->>'TotalItbis')::numeric), 0)             AS "TotalItbis",
                coalesce(sum((totals->>'TotalItbis1')::numeric), 0)            AS "TotalItbis1",
                coalesce(sum((totals->>'TotalItbis2')::numeric), 0)            AS "TotalItbis2",
                coalesce(sum((totals->>'TotalItbis3')::numeric), 0)            AS "TotalItbis3",
                coalesce(sum((totals->>'MontoExento')::numeric), 0)            AS "TotalExempt",
                coalesce(sum((totals->>'TotalItbisRetenido')::numeric), 0)     AS "TotalItbisWithheld",
                coalesce(sum((totals->>'TotalIsrRetencion')::numeric), 0)      AS "TotalIsrWithheld",
                coalesce(sum(monto_total) FILTER (WHERE ecf_type = 34), 0)     AS "TotalCreditNoteAmount",
                coalesce(sum(monto_total) FILTER (WHERE ecf_type = 33), 0)     AS "TotalDebitNoteAmount"
            FROM issued_ecf
            {Where}
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleAsync<FiscalTotalsLookup>(
            new CommandDefinition(sql, new { tenantId, from, to = through }, session.Transaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<FiscalTypeTotalsLookup>> GetTotalsByTypeAsync(
        Guid tenantId, DateOnly from, DateOnly through, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT
                ecf_type::int                                       AS "Type",
                count(*)::int                                       AS "Count",
                coalesce(sum(monto_total), 0)                       AS "TotalAmount",
                coalesce(sum((totals->>'TotalItbis')::numeric), 0)  AS "TotalItbis"
            FROM issued_ecf
            {Where}
            GROUP BY ecf_type
            ORDER BY ecf_type
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<FiscalTypeTotalsLookup>(
            new CommandDefinition(sql, new { tenantId, from, to = through }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
