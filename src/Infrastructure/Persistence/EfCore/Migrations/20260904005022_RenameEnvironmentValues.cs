using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <summary>
    /// Normaliza los valores de ambiente de la DGII que se guardan como texto
    /// (<c>DgiiEnvironment.Name</c>): <c>TestEcf</c> → <c>Test</c>,
    /// <c>CertEcf</c> → <c>Cert</c>. <c>Production</c> no cambia. Solo datos; el
    /// esquema no se toca (las columnas ya son <c>varchar(20)</c>).
    /// </summary>
    public partial class RenameEnvironmentValues : Migration
    {
        // (tabla, columna) que guardan un DgiiEnvironment.Name.
        private static readonly (string Table, string Column)[] Targets =
        [
            ("emitter_profiles", "default_environment"),
            ("certificates", "environment"),
            ("ncf_sequences", "environment"),
            ("issued_ecf", "environment"),
            ("ecf_submission_outbox", "environment"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
            => Remap(migrationBuilder, "TestEcf", "Test", "CertEcf", "Cert");

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            => Remap(migrationBuilder, "Test", "TestEcf", "Cert", "CertEcf");

        private static void Remap(MigrationBuilder builder, string testFrom, string testTo, string certFrom, string certTo)
        {
            foreach (var (table, column) in Targets)
            {
                builder.Sql($"UPDATE {table} SET {column} = '{testTo}' WHERE {column} = '{testFrom}';");
                builder.Sql($"UPDATE {table} SET {column} = '{certTo}' WHERE {column} = '{certFrom}';");
            }
        }
    }
}
