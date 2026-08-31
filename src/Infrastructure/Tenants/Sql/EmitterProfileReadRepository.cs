using Dapper;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Application.Tenants.Interfaces;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Tenants.Sql;

/// <summary>
/// Lectura del perfil fiscal del emisor con Dapper. Columnas con alias entre
/// comillas para casar con el record; <c>phones</c> es <c>text[]</c> y Npgsql lo
/// devuelve como <c>string[]</c>.
/// </summary>
internal sealed class EmitterProfileReadRepository(IDbSession session) : IEmitterProfileReadRepository
{
    public async Task<EmitterProfileDto?> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id                  AS "Id",
                   tenant_id           AS "TenantId",
                   address             AS "Address",
                   municipality        AS "Municipality",
                   province            AS "Province",
                   phones              AS "Phones",
                   email               AS "Email",
                   economic_activity   AS "EconomicActivity",
                   default_environment AS "DefaultEnvironment",
                   created_at          AS "CreatedAt",
                   updated_at          AS "UpdatedAt"
            FROM emitter_profiles
            WHERE tenant_id = @tenantId AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<EmitterProfileDto>(
            new CommandDefinition(sql, new { tenantId }, session.Transaction, cancellationToken: ct));
    }
}
