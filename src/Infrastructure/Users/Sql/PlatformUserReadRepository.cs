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

    public async Task<TenantAccessLookup?> ResolveTenantAccessAsync(
        Guid platformUserId, Guid tenantId, CancellationToken ct = default)
    {
        // Acceso directo (tenant_members) o heredado: owner/admin de la
        // organización dueña del tenant actúa como admin_tenant ahí, sin fila
        // explícita. Ninguno de los dos si el tenant o su organización están
        // suspendidos.
        const string sql =
            """
            SELECT t.id                              AS "TenantId",
                   coalesce(tm.role, 'admin_tenant')  AS "Role"
            FROM tenants t
            LEFT JOIN tenant_members tm
                ON tm.tenant_id = t.id AND tm.platform_user_id = @platformUserId
            LEFT JOIN organization_members om
                ON om.organization_id = t.organization_id
               AND om.platform_user_id = @platformUserId
               AND om.role IN ('owner', 'admin')
            WHERE t.id = @tenantId
              AND t.is_deleted = false
              AND t.status = 'Active'
              AND (tm.platform_user_id IS NOT NULL OR om.platform_user_id IS NOT NULL)
              AND NOT EXISTS (
                  SELECT 1 FROM organizations o
                  WHERE o.id = t.organization_id AND o.status = 'Suspended'
              )
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<TenantAccessLookup>(
            new CommandDefinition(sql, new { platformUserId, tenantId }, session.Transaction, cancellationToken: ct));
    }

    public async Task<TenantAccessLookup?> ResolveDefaultTenantAccessAsync(
        Guid platformUserId, CancellationToken ct = default)
    {
        // El primero por antigüedad con acceso directo; si no hay ninguno, el
        // primer tenant heredado de una organización donde sea owner/admin.
        const string sql =
            """
            SELECT combined.tenant_id AS "TenantId", combined.role AS "Role"
            FROM (
                SELECT t.id AS tenant_id, tm.role AS role, 0 AS priority, tm.created_at AS ordering
                FROM tenant_members tm
                JOIN tenants t ON t.id = tm.tenant_id
                WHERE tm.platform_user_id = @platformUserId
                  AND t.is_deleted = false AND t.status = 'Active'
                  AND NOT EXISTS (
                      SELECT 1 FROM organizations o
                      WHERE o.id = t.organization_id AND o.status = 'Suspended'
                  )

                UNION ALL

                SELECT t.id AS tenant_id, 'admin_tenant' AS role, 1 AS priority, t.created_at AS ordering
                FROM organization_members om
                JOIN tenants t ON t.organization_id = om.organization_id
                WHERE om.platform_user_id = @platformUserId
                  AND om.role IN ('owner', 'admin')
                  AND t.is_deleted = false AND t.status = 'Active'
                  AND NOT EXISTS (
                      SELECT 1 FROM organizations o
                      WHERE o.id = t.organization_id AND o.status = 'Suspended'
                  )
            ) combined
            ORDER BY combined.priority, combined.ordering
            LIMIT 1
            """;

        var connection = await session.GetConnectionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<TenantAccessLookup>(
            new CommandDefinition(sql, new { platformUserId }, session.Transaction, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<OrganizationMembershipLookup>> ListOrganizationMembershipsAsync(
        Guid platformUserId, CancellationToken ct = default)
    {
        // Fila plana (organización × tenant accesible); se agrupa acá porque
        // Dapper no arma jerarquías de más de un nivel sin un splitOn frágil.
        // owner/admin ven todos los tenants activos de su organización
        // (heredado, admin_tenant salvo que tengan una fila propia en
        // tenant_members); member solo los que tiene explícitos. Una
        // organización sin tenants todavía sale con TenantId nulo.
        const string sql =
            """
            SELECT o.id                              AS "OrganizationId",
                   o.name                             AS "OrganizationName",
                   o.slug                             AS "OrganizationSlug",
                   o.plan                             AS "OrganizationPlan",
                   o.status                           AS "OrganizationStatus",
                   om.role                             AS "OrgRole",
                   t.id                                AS "TenantId",
                   t.legal_name                        AS "TenantName",
                   coalesce(tm.role, 'admin_tenant')   AS "TenantRole"
            FROM organization_members om
            JOIN organizations o ON o.id = om.organization_id AND o.is_deleted = false
            LEFT JOIN tenants t
                ON t.organization_id = o.id
               AND t.is_deleted = false
               AND t.status = 'Active'
               AND (
                     om.role IN ('owner', 'admin')
                  OR EXISTS (
                        SELECT 1 FROM tenant_members tm2
                        WHERE tm2.tenant_id = t.id AND tm2.platform_user_id = @platformUserId
                     )
                   )
            LEFT JOIN tenant_members tm ON tm.tenant_id = t.id AND tm.platform_user_id = @platformUserId
            WHERE om.platform_user_id = @platformUserId
            ORDER BY o.created_at, t.created_at
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<MembershipRow>(
            new CommandDefinition(sql, new { platformUserId }, session.Transaction, cancellationToken: ct));

        return [.. rows
            .GroupBy(r => (r.OrganizationId, r.OrganizationName, r.OrganizationSlug, r.OrganizationPlan, r.OrganizationStatus, r.OrgRole))
            .Select(g => new OrganizationMembershipLookup(
                g.Key.OrganizationId,
                g.Key.OrganizationName,
                g.Key.OrganizationSlug,
                g.Key.OrganizationPlan,
                g.Key.OrganizationStatus,
                g.Key.OrgRole,
                [.. g.Where(r => r.TenantId is not null)
                    .Select(r => new OrganizationTenantLookup(r.TenantId!.Value, r.TenantName!, r.TenantRole!))]))];
    }

    public async Task<IReadOnlyList<OrganizationTenantLookup>> ListDirectTenantAccessAsync(
        Guid platformUserId, CancellationToken ct = default)
    {
        // La misma rama de acceso directo que ya arma la parte "tenant_members"
        // de ResolveDefaultTenantAccessAsync, pero sin LIMIT 1: acá se necesitan
        // todos, no solo el que gana la prioridad de default.
        const string sql =
            """
            SELECT t.id AS "TenantId", t.legal_name AS "TenantName", tm.role AS "Role"
            FROM tenant_members tm
            JOIN tenants t ON t.id = tm.tenant_id
            WHERE tm.platform_user_id = @platformUserId
              AND t.is_deleted = false AND t.status = 'Active'
              AND NOT EXISTS (
                  SELECT 1 FROM organizations o
                  WHERE o.id = t.organization_id AND o.status = 'Suspended'
              )
            ORDER BY tm.created_at
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<OrganizationTenantLookup>(
            new CommandDefinition(sql, new { platformUserId }, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }

    private sealed record MembershipRow(
        Guid OrganizationId,
        string OrganizationName,
        string OrganizationSlug,
        string OrganizationPlan,
        string OrganizationStatus,
        string OrgRole,
        Guid? TenantId,
        string? TenantName,
        string? TenantRole);

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
        // "Operador" es el rol admin_sistema, no "sin tenant" — desde Fase 2 un
        // miembro de organización sin tenant fijo (CreateOrganizationMember)
        // también tiene tenant_id null, y no es operador.
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
            WHERE role = 'admin_sistema' AND is_deleted = false
            ORDER BY created_at DESC
            """;

        var connection = await session.GetConnectionAsync(ct);

        var rows = await connection.QueryAsync<PlatformUserDto>(
            new CommandDefinition(sql, session.Transaction, cancellationToken: ct));

        return [.. rows];
    }
}
