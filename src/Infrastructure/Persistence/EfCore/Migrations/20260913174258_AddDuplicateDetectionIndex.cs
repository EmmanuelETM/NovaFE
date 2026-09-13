using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddDuplicateDetectionIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_issued_ecf_tenant_id_environment_ecf_type_buyer_rnc_monto_t",
                table: "issued_ecf",
                columns: new[] { "tenant_id", "environment", "ecf_type", "buyer_rnc", "monto_total", "issue_date" },
                filter: "buyer_rnc is not null and is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_issued_ecf_tenant_id_environment_ecf_type_buyer_rnc_monto_t",
                table: "issued_ecf");
        }
    }
}
