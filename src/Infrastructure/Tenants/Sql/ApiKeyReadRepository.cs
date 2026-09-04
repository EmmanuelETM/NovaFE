using Dapper;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Tenants.Sql;

/// <summary>
/// Lectura de las API keys con Dapper. Columnas con alias entre comillas para
/// casar con el record; sin interceptores, así que el <c>WHERE is_deleted</c> va
/// explícito.
/// </summary>
internal sealed class ApiKeyReadRepository(IDbSession session) : IApiKeyReadRepository
{
    public async Task<IReadOnlyList<ApiKeyDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id            AS "Id",
                   tenant_id     AS "TenantId",
                   prefix        AS "Prefix",
                   label         AS "Label",
                   environment   AS "Environment",
                   expires_at    AS "ExpiresAt",
                   revoked_at    AS "RevokedAt",
                   last_used_at  AS "LastUsedAt",
                   created_at    AS "CreatedAt"
            FROM api_keys
            WHERE tenant_id = @tenantId AND is_deleted = false
            ORDER BY created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<ApiKeyDto>(
            new CommandDefinition(sql, new { tenantId }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }

    public async Task<ApiKeyLookup?> FindByHashAsync(string keyHash, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id          AS "Id",
                   tenant_id   AS "TenantId",
                   environment AS "Environment",
                   expires_at  AS "ExpiresAt",
                   revoked_at  AS "RevokedAt"
            FROM api_keys
            WHERE key_hash = @keyHash AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<ApiKeyLookup>(
            new CommandDefinition(sql, new { keyHash }, session.Transaction, cancellationToken: ct));
    }
}
