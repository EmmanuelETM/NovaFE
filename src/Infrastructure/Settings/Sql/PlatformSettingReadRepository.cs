using Dapper;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Settings.Sql;

/// <summary>
/// Lectura de los overrides con Dapper. Columnas con alias entre comillas para
/// casar con <see cref="PlatformSettingRecord"/>; <c>updated_*</c> cae a
/// <c>created_*</c> (regla de nombrar al autor, no solo su id).
/// </summary>
internal sealed class PlatformSettingReadRepository(IDbSession session) : IPlatformSettingReadRepository
{
    public async Task<IReadOnlyList<PlatformSettingRecord>> ListAsync(CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT key                              AS "Key",
                   environment                      AS "Environment",
                   value                            AS "Value",
                   coalesce(updated_at, created_at) AS "UpdatedAt",
                   coalesce(updated_by, created_by) AS "UpdatedBy"
            FROM platform_settings
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<PlatformSettingRecord>(
            new CommandDefinition(sql, transaction: session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
