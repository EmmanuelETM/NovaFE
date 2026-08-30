using System;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddNcfSequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ncf_sequences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ecf_type = table.Column<short>(type: "smallint", nullable: false),
                    series = table.Column<string>(type: "char(1)", nullable: false),
                    range_from = table.Column<long>(type: "bigint", nullable: false),
                    range_to = table.Column<long>(type: "bigint", nullable: false),
                    next = table.Column<long>(type: "bigint", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_ncf_sequences", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ncf_sequences_tenant_id_environment_ecf_type_series",
                table: "ncf_sequences",
                columns: new[] { "tenant_id", "environment", "ecf_type", "series" },
                unique: true,
                filter: "active and is_deleted = false");

            // La tabla guarda datos de un tenant: aislamiento por RLS.
            RowLevelSecurity.Enable(migrationBuilder, "ncf_sequences");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RowLevelSecurity.Disable(migrationBuilder, "ncf_sequences");

            migrationBuilder.DropTable(
                name: "ncf_sequences");
        }
    }
}
