using Dapper;
using NovaFE.Application.Ecf.Contracts;
using NovaFE.Application.Ecf.Interfaces;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Ecf.Sql;

/// <summary>
/// Lectura de comprobantes emitidos con Dapper. Siempre filtra por <c>tenant_id</c>
/// (defensa en profundidad). Las consultas de detalle y listado <b>no</b> traen el
/// XML (<c>ecf_xml</c> / <c>rfce_xml</c> son columnas grandes); eso lo sirve
/// <see cref="GetXmlAsync"/>.
/// </summary>
internal sealed class EcfReadRepository(IDbSession session) : IEcfReadRepository
{
    private const string DetailColumns =
        """
        id                     AS "Id",
        status                 AS "Status",
        encf                   AS "Encf",
        ecf_type::int          AS "Type",
        environment            AS "Environment",
        sequence_expires_on    AS "SequenceExpiresOn",
        issue_date             AS "IssueDate",
        created_at             AS "IssuedAt",
        signed_at              AS "SignedAt",
        security_code          AS "SecurityCode",
        qr_url                 AS "QrUrl",
        submits_rfce           AS "SubmitsRfce",
        internal_invoice_number AS "InternalNumber",
        buyer_rnc              AS "BuyerRnc",
        buyer_name             AS "BuyerName",
        totals                 AS "Totals",
        CASE WHEN expected_conditional_acceptance
             THEN 'Los montos declarados no cuadran dentro de la tolerancia; la DGII podría aceptar el comprobante de forma condicional.'
             ELSE NULL
        END                    AS "ToleranceWarning"
        """;

    public async Task<EcfDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {DetailColumns}
            FROM issued_ecf
            WHERE id = @id AND tenant_id = @tenantId AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<EcfDto>(
            new CommandDefinition(sql, new { id, tenantId }, session.Transaction, cancellationToken: ct));
    }

    public async Task<string?> GetXmlAsync(Guid id, Guid tenantId, bool rfce, CancellationToken ct = default)
    {
        var column = rfce ? "rfce_xml" : "ecf_xml";
        var sql =
            $"""
            SELECT {column}
            FROM issued_ecf
            WHERE id = @id AND tenant_id = @tenantId AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(sql, new { id, tenantId }, session.Transaction, cancellationToken: ct));
    }

    public async Task<Guid?> FindByInternalNumberAsync(
        Guid tenantId, string internalNumber, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id
            FROM issued_ecf
            WHERE tenant_id = @tenantId
              AND internal_invoice_number = @internalNumber
              AND is_deleted = false
            LIMIT 1
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { tenantId, internalNumber }, session.Transaction, cancellationToken: ct));
    }

    public async Task<PagedResult<EcfSummaryDto>> ListAsync(
        Guid tenantId, EcfListFilter filter, CancellationToken ct = default)
    {
        // Los ::tipo hacen explícito el tipo de cada parámetro cuando el valor es
        // NULL (si no, Postgres no lo infiere del `IS NULL` — error 42P08).
        const string where =
            """
            WHERE tenant_id = @tenantId
              AND is_deleted = false
              AND (@type::int IS NULL OR ecf_type = @type::int)
              AND (@status::text IS NULL OR status = @status::text)
              AND (@from::date IS NULL OR issue_date >= @from::date)
              AND (@to::date IS NULL OR issue_date <= @to::date)
              AND (@pattern::text IS NULL
                   OR encf ILIKE @pattern::text
                   OR buyer_rnc ILIKE @pattern::text
                   OR buyer_name ILIKE @pattern::text)
            """;

        var countSql = $"SELECT count(*) FROM issued_ecf {where}";
        var pageSql =
            $"""
            SELECT id          AS "Id",
                   status      AS "Status",
                   encf        AS "Encf",
                   ecf_type::int AS "Type",
                   issue_date  AS "IssueDate",
                   monto_total AS "MontoTotal",
                   buyer_rnc  AS "BuyerRnc",
                   buyer_name AS "BuyerName",
                   created_at AS "CreatedAt"
            FROM issued_ecf
            {where}
            ORDER BY created_at DESC
            LIMIT @take OFFSET @skip
            """;

        var parameters = new
        {
            tenantId,
            type = filter.Type,
            status = string.IsNullOrWhiteSpace(filter.Status) ? null : filter.Status.Trim(),
            from = filter.From,
            to = filter.To,
            pattern = string.IsNullOrWhiteSpace(filter.Search) ? null : $"%{filter.Search.Trim()}%",
            take = filter.PageSize,
            skip = filter.Skip,
        };

        var connection = await session.GetConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, session.Transaction, cancellationToken: ct));

        var items = await connection.QueryAsync<EcfSummaryDto>(
            new CommandDefinition(pageSql, parameters, session.Transaction, cancellationToken: ct));

        return new PagedResult<EcfSummaryDto>(items.AsList(), total, filter.Page, filter.PageSize);
    }
}
