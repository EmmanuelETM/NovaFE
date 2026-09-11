using System;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_setting_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    previous_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_setting_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenant_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_settings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_setting_changes_tenant_id_key_changed_at",
                table: "tenant_setting_changes",
                columns: new[] { "tenant_id", "key", "changed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_tenant_settings_tenant_id_key_environment",
                table: "tenant_settings",
                columns: new[] { "tenant_id", "key", "environment" },
                unique: true);

            // Ambas tablas llevan datos de un contribuyente → RLS por tenant
            // (docs/multi-tenancy.md). En producción la app conecta con un rol
            // sin BYPASSRLS; en local/tests la garantía es el filtro global de EF.
            RowLevelSecurity.Enable(migrationBuilder, "tenant_settings");
            RowLevelSecurity.Enable(migrationBuilder, "tenant_setting_changes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RowLevelSecurity.Disable(migrationBuilder, "tenant_setting_changes");
            RowLevelSecurity.Disable(migrationBuilder, "tenant_settings");

            migrationBuilder.DropTable(
                name: "tenant_setting_changes");

            migrationBuilder.DropTable(
                name: "tenant_settings");
        }
    }
}
