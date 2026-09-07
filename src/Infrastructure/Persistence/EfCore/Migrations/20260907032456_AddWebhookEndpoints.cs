using System;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookEndpoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    secret = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    events = table.Column<string[]>(type: "text[]", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    consecutive_failures = table.Column<int>(type: "integer", nullable: false),
                    disabled_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    disabled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_endpoints", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_webhook_endpoints_tenant_id",
                table: "webhook_endpoints",
                column: "tenant_id");

            // webhook_endpoints es ITenantOwned → aislamiento por tenant en la base.
            RowLevelSecurity.Enable(migrationBuilder, "webhook_endpoints");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RowLevelSecurity.Disable(migrationBuilder, "webhook_endpoints");

            migrationBuilder.DropTable(
                name: "webhook_endpoints");
        }
    }
}
