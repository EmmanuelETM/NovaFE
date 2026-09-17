using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class MoveTenantPlanToOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "plan",
                table: "tenants");

            // Default para organizaciones que ya existan (backfill de Fase 1):
            // el plan/estado se movió acá, no había columna antes.
            migrationBuilder.AddColumn<string>(
                name: "plan",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Developer");

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "organizations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Active");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "plan",
                table: "organizations");

            migrationBuilder.DropColumn(
                name: "status",
                table: "organizations");

            migrationBuilder.AddColumn<string>(
                name: "plan",
                table: "tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Developer");
        }
    }
}
