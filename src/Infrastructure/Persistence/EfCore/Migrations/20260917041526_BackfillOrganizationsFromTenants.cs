using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <summary>
    /// Backfill de la Fase 1 del refactor jerárquico (<c>docs/multi-tenancy-hierarchy.md</c>):
    /// crea una <c>Organization</c> 1:1 por cada <c>Tenant</c> todavía sin
    /// organización, y espeja los <c>platform_users</c> existentes a
    /// <c>tenant_members</c>/<c>organization_members</c> sin tocar sus filas
    /// (<c>PlatformUser.TenantId</c>/<c>Role</c> siguen siendo la fuente de
    /// verdad hasta la Fase 2). Idempotente: solo toca tenants con
    /// <c>organization_id IS NULL</c> y usa <c>ON CONFLICT DO NOTHING</c> en las
    /// membresías, así que correrla dos veces no duplica nada.
    /// </summary>
    public partial class BackfillOrganizationsFromTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $$
                DECLARE
                    r RECORD;
                    new_org_id uuid;
                    base_slug text;
                BEGIN
                    FOR r IN SELECT id, legal_name FROM tenants
                             WHERE organization_id IS NULL AND is_deleted = false
                    LOOP
                        new_org_id := gen_random_uuid();

                        base_slug := trim(both '-' from lower(regexp_replace(trim(r.legal_name), '[^a-zA-Z0-9]+', '-', 'g')));
                        IF base_slug = '' THEN
                            base_slug := 'org';
                        END IF;

                        INSERT INTO organizations (id, name, slug, created_at, is_deleted)
                        VALUES (
                            new_org_id,
                            r.legal_name,
                            base_slug || '-' || substr(replace(r.id::text, '-', ''), 1, 8),
                            now(),
                            false
                        );

                        UPDATE tenants SET organization_id = new_org_id WHERE id = r.id;

                        -- El primer admin_tenant de cada tenant queda como owner de la
                        -- organización nueva; el resto (emisor/consultor) como member.
                        INSERT INTO organization_members (id, organization_id, platform_user_id, role, created_at)
                        SELECT gen_random_uuid(), new_org_id, pu.id,
                               CASE WHEN pu.role = 'admin_tenant' THEN 'owner' ELSE 'member' END,
                               now()
                        FROM platform_users pu
                        WHERE pu.tenant_id = r.id AND pu.is_deleted = false
                        ON CONFLICT (organization_id, platform_user_id) DO NOTHING;
                    END LOOP;

                    -- tenant_members espeja el rol que ya tenía cada usuario en su
                    -- único tenant (PlatformUser.Role), para todos los tenants, no
                    -- solo los recién backfilleados (corrida idempotente).
                    INSERT INTO tenant_members (id, tenant_id, platform_user_id, role, created_at)
                    SELECT gen_random_uuid(), pu.tenant_id, pu.id, pu.role, now()
                    FROM platform_users pu
                    WHERE pu.tenant_id IS NOT NULL AND pu.is_deleted = false
                    ON CONFLICT (tenant_id, platform_user_id) DO NOTHING;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Backfill de datos de desarrollo/sandbox (Fase 1, pre-producción): sin
            // reversa. Revertir también AddOrganizations elimina las tablas nuevas
            // por completo si hace falta deshacer todo el refactor.
        }
    }
}
