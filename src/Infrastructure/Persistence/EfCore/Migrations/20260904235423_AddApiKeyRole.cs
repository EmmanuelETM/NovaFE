using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <summary>
    /// RF-14.5 — RBAC por key. Columna nueva en <c>api_keys</c>; las filas que ya
    /// existan (M14 slice 1/2, sin rol) se asumen <c>admin_tenant</c> — el rol más
    /// amplio, para no dejar sin acceso a ninguna key acuñada antes de este slice.
    /// El dominio siempre lo escribe explícito; el default de servidor solo cubre
    /// datos preexistentes.
    /// </summary>
    public partial class AddApiKeyRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "api_keys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "admin_tenant");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "role",
                table: "api_keys");
        }
    }
}
