using Dapper;
using NovaFE.Application.Settings.Contracts;
using NovaFE.Application.Settings.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Settings.Sql;

/// <summary>
/// Lectura de los overrides de tenant con Dapper. El <c>WHERE tenant_id</c> va
/// explícito como defensa en profundidad (la RLS de <c>tenant_settings</c> ya
/// acota en producción). El "quién/cuándo" sale de la última entrada de
/// <c>tenant_setting_changes</c>.
/// </summary>
internal sealed class TenantSettingReadRepository(IDbSession session) : ITenantSettingReadRepository
{
    public async Task<IReadOnlyList<TenantSettingRecord>> ListAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT s.key                                                 AS "Key",
                   s.environment                                         AS "Environment",
                   s.value                                               AS "Value",
                   coalesce(last.changed_at, s.updated_at, s.created_at)  AS "UpdatedAt",
                   coalesce(last.changed_by, s.updated_by, s.created_by)  AS "UpdatedBy"
            FROM tenant_settings s
            LEFT JOIN LATERAL (
                SELECT c.changed_at, c.changed_by
                FROM tenant_setting_changes c
                WHERE c.tenant_id = s.tenant_id AND c.key = s.key
                ORDER BY c.changed_at DESC
                LIMIT 1
            ) last ON true
            WHERE s.tenant_id = @tenantId
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<TenantSettingRecord>(
            new CommandDefinition(sql, new { tenantId }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }

    public async Task<IReadOnlyList<TenantSettingChangeDto>> ListChangesByKeyAsync(
        Guid tenantId, string key, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT previous_value AS "PreviousValue",
                   new_value      AS "NewValue",
                   changed_at     AS "ChangedAt",
                   changed_by     AS "ChangedBy"
            FROM tenant_setting_changes
            WHERE tenant_id = @tenantId AND key = @key
            ORDER BY changed_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<TenantSettingChangeDto>(
            new CommandDefinition(sql, new { tenantId, key }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
