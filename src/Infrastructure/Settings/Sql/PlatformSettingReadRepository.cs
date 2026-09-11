using Dapper;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Settings.Sql;

/// <summary>
/// Lectura de los overrides con Dapper. El "quién/cuándo" sale de la última
/// entrada de <c>platform_setting_changes</c> (donde el autor ya está como
/// correo); las columnas de auditoría de la propia fila son la red.
/// </summary>
internal sealed class PlatformSettingReadRepository(IDbSession session) : IPlatformSettingReadRepository
{
    public async Task<IReadOnlyList<PlatformSettingRecord>> ListAsync(CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT s.key                                          AS "Key",
                   s.environment                                  AS "Environment",
                   s.value                                        AS "Value",
                   coalesce(last.changed_at, s.updated_at, s.created_at) AS "UpdatedAt",
                   coalesce(last.changed_by, s.updated_by, s.created_by) AS "UpdatedBy"
            FROM platform_settings s
            LEFT JOIN LATERAL (
                SELECT c.changed_at, c.changed_by
                FROM platform_setting_changes c
                WHERE c.key = s.key
                ORDER BY c.changed_at DESC
                LIMIT 1
            ) last ON true
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<PlatformSettingRecord>(
            new CommandDefinition(sql, transaction: session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
