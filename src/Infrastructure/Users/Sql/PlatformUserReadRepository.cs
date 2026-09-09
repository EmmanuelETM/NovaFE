using Dapper;
using NovaFE.Application.Users.Contracts;
using NovaFE.Application.Users.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Users.Sql;

/// <summary>
/// Lectura de los usuarios de la plataforma con Dapper. Columnas con alias entre
/// comillas para casar con el record; sin interceptores, así que el
/// <c>WHERE is_deleted</c> va explícito.
/// </summary>
internal sealed class PlatformUserReadRepository(IDbSession session) : IPlatformUserReadRepository
{
    public async Task<PlatformUserLookup?> ResolveAsync(
        string? authUserId,
        string email,
        CancellationToken ct = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var trimmedAuthId = string.IsNullOrWhiteSpace(authUserId) ? null : authUserId.Trim();

        // Preferencia por auth_user_id (enlazado tras el primer login); si no,
        // por correo (usuario recién aprovisionado que entra por primera vez).
        const string sql =
            """
            SELECT id           AS "Id",
                   tenant_id    AS "TenantId",
                   role         AS "Role",
                   email        AS "Email",
                   auth_user_id AS "AuthUserId",
                   revoked_at   AS "RevokedAt"
            FROM platform_users
            WHERE is_deleted = false
              AND (
                    (@authUserId IS NOT NULL AND auth_user_id = @authUserId)
                 OR email = @email
                  )
            ORDER BY (auth_user_id = @authUserId) DESC NULLS LAST
            LIMIT 1
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<PlatformUserLookup>(
            new CommandDefinition(
                sql,
                new { authUserId = trimmedAuthId, email = normalizedEmail },
                session.Transaction,
                cancellationToken: ct));
    }

    public async Task<PlatformUserDto?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id           AS "Id",
                   email        AS "Email",
                   role         AS "Role",
                   tenant_id    AS "TenantId",
                   (auth_user_id IS NOT NULL) AS "AuthLinked",
                   revoked_at   AS "RevokedAt",
                   created_at   AS "CreatedAt"
            FROM platform_users
            WHERE id = @id AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<PlatformUserDto>(
            new CommandDefinition(sql, new { id }, session.Transaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<PlatformUserDto>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id           AS "Id",
                   email        AS "Email",
                   role         AS "Role",
                   tenant_id    AS "TenantId",
                   (auth_user_id IS NOT NULL) AS "AuthLinked",
                   revoked_at   AS "RevokedAt",
                   created_at   AS "CreatedAt"
            FROM platform_users
            WHERE tenant_id = @tenantId AND is_deleted = false
            ORDER BY created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<PlatformUserDto>(
            new CommandDefinition(sql, new { tenantId }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }

    public async Task<IReadOnlyList<PlatformUserDto>> ListOperatorsAsync(CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id           AS "Id",
                   email        AS "Email",
                   role         AS "Role",
                   tenant_id    AS "TenantId",
                   (auth_user_id IS NOT NULL) AS "AuthLinked",
                   revoked_at   AS "RevokedAt",
                   created_at   AS "CreatedAt"
            FROM platform_users
            WHERE tenant_id IS NULL AND is_deleted = false
            ORDER BY created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<PlatformUserDto>(
            new CommandDefinition(sql, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
