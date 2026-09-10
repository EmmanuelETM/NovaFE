using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <summary>
    /// Motor de settings de plataforma (docs/configuration.md). Tres tablas de
    /// sistema, <b>sin RLS</b> (las administra el operador, no un tenant):
    /// <c>platform_settings</c> (overrides), <c>platform_setting_changes</c>
    /// (bitácora append-only) y <c>settings_generation</c> (contador de una fila
    /// que cualquier escritura incrementa; el poller de cada instancia lo consulta
    /// para saber si recargar su snapshot).
    /// </summary>
    public partial class AddPlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_setting_changes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    previous_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    new_value = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    changed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_setting_changes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_settings",
                columns: table => new
                {
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
                    table.PrimaryKey("pk_platform_settings", x => new { x.key, x.environment });
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_setting_changes_key_changed_at",
                table: "platform_setting_changes",
                columns: new[] { "key", "changed_at" });

            // Contador de generación: fila única (id = 1), sin entidad EF.
            migrationBuilder.Sql(
                """
                CREATE TABLE settings_generation (
                    id    integer PRIMARY KEY CHECK (id = 1),
                    value bigint  NOT NULL
                );
                INSERT INTO settings_generation (id, value) VALUES (1, 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS settings_generation;");

            migrationBuilder.DropTable(
                name: "platform_setting_changes");

            migrationBuilder.DropTable(
                name: "platform_settings");
        }
    }
}
