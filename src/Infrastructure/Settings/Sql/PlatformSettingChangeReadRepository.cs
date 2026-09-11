using Dapper;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Settings.Sql;

/// <summary>Lectura de la bitácora de cambios de un setting con Dapper.</summary>
internal sealed class PlatformSettingChangeReadRepository(IDbSession session)
    : IPlatformSettingChangeReadRepository
{
    public async Task<IReadOnlyList<PlatformSettingChangeDto>> ListByKeyAsync(
        string key, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT previous_value AS "PreviousValue",
                   new_value      AS "NewValue",
                   changed_at     AS "ChangedAt",
                   changed_by     AS "ChangedBy"
            FROM platform_setting_changes
            WHERE key = @key
            ORDER BY changed_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<PlatformSettingChangeDto>(
            new CommandDefinition(sql, new { key }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
