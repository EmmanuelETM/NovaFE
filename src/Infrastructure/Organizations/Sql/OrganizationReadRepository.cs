using Dapper;
using NovaFE.Application.Organizations.Contracts;
using NovaFE.Application.Organizations.Interfaces;
using NovaFE.Application.Tenants.Contracts;
using NovaFE.Domain.Common;
using NovaFE.Infrastructure.Persistence.Sql;

namespace NovaFE.Infrastructure.Organizations.Sql;

/// <summary>
/// Lectura de organizaciones con Dapper. Las columnas van con alias entre
/// comillas para que Dapper case cada columna con el parámetro del record de
/// lectura, igual que <c>TenantReadRepository</c>.
/// </summary>
internal sealed class OrganizationReadRepository(IDbSession session) : IOrganizationReadRepository
{
    public async Task<OrganizationDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT id         AS "Id",
                   name       AS "Name",
                   slug       AS "Slug",
                   created_at AS "CreatedAt"
            FROM organizations
            WHERE id = @id AND is_deleted = false
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<OrganizationDto>(
            new CommandDefinition(sql, new { id }, session.Transaction, cancellationToken: ct));
    }

    public async Task<PagedResult<OrganizationSummaryDto>> ListAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken ct = default)
    {
        const string filter =
            """
            WHERE is_deleted = false
              AND (@pattern IS NULL OR name ILIKE @pattern OR slug ILIKE @pattern)
            """;

        var countSql = $"SELECT count(*) FROM organizations {filter}";
        var pageSql =
            $"""
            SELECT id   AS "Id",
                   name AS "Name",
                   slug AS "Slug"
            FROM organizations
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

        var items = await connection.QueryAsync<OrganizationSummaryDto>(
            new CommandDefinition(pageSql, parameters, session.Transaction, cancellationToken: ct));

        return new PagedResult<OrganizationSummaryDto>(items.AsList(), total, page, pageSize);
    }

    public async Task<IReadOnlyList<OrganizationMemberDto>> ListMembersAsync(
        Guid organizationId,
        CancellationToken ct = default)
    {
        const string sql =
            """
            SELECT pu.id         AS "PlatformUserId",
                   pu.email      AS "Email",
                   om.role       AS "Role",
                   om.created_at AS "CreatedAt"
            FROM organization_members om
            JOIN platform_users pu ON pu.id = om.platform_user_id
            WHERE om.organization_id = @organizationId
            ORDER BY om.created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var members = await connection.QueryAsync<OrganizationMemberDto>(
            new CommandDefinition(sql, new { organizationId }, session.Transaction, cancellationToken: ct));

        return [.. members];
    }

    public async Task<PagedResult<TenantSummaryDto>> ListTenantsAsync(
        Guid organizationId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        const string filter = "WHERE organization_id = @organizationId AND is_deleted = false";

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
            organizationId,
            take = pageSize,
            skip = (page - 1) * pageSize,
        };

        var connection = await session.GetConnectionAsync(ct);

        var total = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, session.Transaction, cancellationToken: ct));

        var items = await connection.QueryAsync<TenantSummaryDto>(
            new CommandDefinition(pageSql, parameters, session.Transaction, cancellationToken: ct));

        return new PagedResult<TenantSummaryDto>(items.AsList(), total, page, pageSize);
    }
}
