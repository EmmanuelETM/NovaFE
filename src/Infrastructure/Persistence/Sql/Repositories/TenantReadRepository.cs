using Dapper;
using NovaFE.Application.Tenants;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Domain.Common;

namespace NovaFE.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// Lectura de contribuyentes con Dapper. Las columnas van con alias entre
/// comillas (<c>legal_name AS "LegalName"</c>) para que Dapper case cada columna
/// con el parámetro del record de lectura; sin el alias, Dapper busca un
/// constructor con parámetros llamados <c>legal_name</c>.
/// </summary>
internal sealed class TenantReadRepository(IDbSession session) : ITenantReadRepository
{
    public async Task<TenantDetail?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id            AS "Id",
                   rnc           AS "Rnc",
                   legal_name    AS "LegalName",
                   trade_name    AS "TradeName",
                   plan          AS "Plan",
                   status        AS "Status",
                   created_at    AS "CreatedAt"
            FROM tenants
            WHERE id = @id AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<TenantDetail>(
            new CommandDefinition(sql, new { id }, session.Transaction, cancellationToken: ct));
    }

    public async Task<PagedResult<TenantSummary>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        const string filter =
            """
            WHERE is_deleted = false
              AND (@pattern IS NULL OR rnc ILIKE @pattern OR legal_name ILIKE @pattern)
            """;

        var countSql = $"SELECT count(*) FROM tenants {filter}";
        var pageSql =
            $"""
            SELECT id         AS "Id",
                   rnc        AS "Rnc",
                   legal_name AS "LegalName",
                   plan       AS "Plan",
                   status     AS "Status"
            FROM tenants
            {filter}
            ORDER BY created_at DESC
            LIMIT @take OFFSET @skip
            """;

        var parameters = new
        {
            pattern = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%",
            take = pageSize,
            skip = (page - 1) * pageSize,
        };

        var connection = await session.GetConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, session.Transaction, cancellationToken: ct));

        var items = await connection.QueryAsync<TenantSummary>(
            new CommandDefinition(pageSql, parameters, session.Transaction, cancellationToken: ct));

        return new PagedResult<TenantSummary>(items.AsList(), total, page, pageSize);
    }
}
