using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <summary>
    /// La API key ata su ambiente de la DGII (la credencial es el selector de
    /// ambiente). Columna nueva en <c>api_keys</c>; las filas que ya existan se
    /// asumen de <c>Test</c> (M14 slice 1 no tenía ambiente). El dominio siempre lo
    /// escribe explícito; el default de servidor solo cubre datos preexistentes.
    /// </summary>
    public partial class AddApiKeyEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "environment",
                table: "api_keys",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Test");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "environment",
                table: "api_keys");
        }
    }
}
