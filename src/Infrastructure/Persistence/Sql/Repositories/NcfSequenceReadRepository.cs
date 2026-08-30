using Dapper;
using NovaFE.Application.Common.Interfaces;
using NovaFE.Application.Sequences.Interfaces;
using NovaFE.Application.Sequences.ReadModels;

namespace NovaFE.Infrastructure.Persistence.Sql.Repositories;

/// <summary>
/// Lectura de rangos de secuencias con Dapper. Filtra por <c>tenant_id</c>
/// explícitamente (en local/tests la app corre como superusuario y RLS no aplica).
/// La capacidad y el stock restante se calculan en SQL.
/// </summary>
internal sealed class NcfSequenceReadRepository(
    IDbSession session,
    ICurrentTenant currentTenant) : INcfSequenceReadRepository
{
    private const string Columns =
        """
        id                                  AS "Id",
        environment                         AS "Environment",
        ecf_type::int                       AS "Type",
        series                              AS "Series",
        range_from                          AS "RangeFrom",
        range_to                            AS "RangeTo",
        "next"                              AS "Next",
        (range_to - range_from + 1)         AS "Capacity",
        greatest(range_to - "next" + 1, 0)  AS "Remaining",
        expires_on                          AS "ExpiresOn",
        active                              AS "Active",
        created_at                          AS "CreatedAt"
        """;

    public async Task<NcfSequenceView?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {Columns}
            FROM ncf_sequences
            WHERE id = @id AND tenant_id = @tenantId AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<NcfSequenceView>(
            new CommandDefinition(
                sql,
                new { id, tenantId = currentTenant.TenantId },
                session.Transaction,
                cancellationToken: ct));
    }

    public async Task<IReadOnlyList<NcfSequenceView>> ListAsync(CancellationToken ct = default)
    {
        var sql =
            $"""
            SELECT {Columns}
            FROM ncf_sequences
            WHERE tenant_id = @tenantId AND is_deleted = false
            ORDER BY environment, ecf_type, series
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<NcfSequenceView>(
            new CommandDefinition(
                sql,
                new { tenantId = currentTenant.TenantId },
                session.Transaction,
                cancellationToken: ct));

        return rows.AsList();
    }
}
