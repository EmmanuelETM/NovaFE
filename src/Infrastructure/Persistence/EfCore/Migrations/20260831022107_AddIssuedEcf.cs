using System;
using NovaFE.Infrastructure.Persistence.EfCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddIssuedEcf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issued_ecf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ecf_type = table.Column<short>(type: "smallint", nullable: false),
                    environment = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    encf = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    sequence_expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: false),
                    internal_invoice_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    buyer_rnc = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    buyer_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    totals = table.Column<string>(type: "jsonb", nullable: false),
                    monto_total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    expected_conditional_acceptance = table.Column<bool>(type: "boolean", nullable: false),
                    signed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    signature_value = table.Column<string>(type: "text", nullable: false),
                    security_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    document_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    qr_url = table.Column<string>(type: "text", nullable: false),
                    submits_rfce = table.Column<bool>(type: "boolean", nullable: false),
                    ecf_xml = table.Column<string>(type: "text", nullable: false),
                    rfce_xml = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_issued_ecf", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issued_ecf_tenant_id_created_at",
                table: "issued_ecf",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_ecf_tenant_id_encf",
                table: "issued_ecf",
                columns: new[] { "tenant_id", "encf" });

            migrationBuilder.CreateIndex(
                name: "ix_issued_ecf_tenant_id_internal_invoice_number",
                table: "issued_ecf",
                columns: new[] { "tenant_id", "internal_invoice_number" },
                unique: true,
                filter: "internal_invoice_number is not null and is_deleted = false");

            // La tabla guarda comprobantes de un tenant: aislamiento por RLS.
            RowLevelSecurity.Enable(migrationBuilder, "issued_ecf");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            RowLevelSecurity.Disable(migrationBuilder, "issued_ecf");

            migrationBuilder.DropTable(
                name: "issued_ecf");
        }
    }
}
