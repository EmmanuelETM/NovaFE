using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NovaFE.Infrastructure.Persistence.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class AddEcfSubmissionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dgii_messages",
                table: "issued_ecf",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dgii_processed_at",
                table: "issued_ecf",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "dgii_status_code",
                table: "issued_ecf",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sequence_usable",
                table: "issued_ecf",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "submission_attempts",
                table: "issued_ecf",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "submitted_at",
                table: "issued_ecf",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "track_id",
                table: "issued_ecf",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_issued_ecf_tenant_id_status",
                table: "issued_ecf",
                columns: new[] { "tenant_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_issued_ecf_tenant_id_status",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "dgii_messages",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "dgii_processed_at",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "dgii_status_code",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "sequence_usable",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "submission_attempts",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "submitted_at",
                table: "issued_ecf");

            migrationBuilder.DropColumn(
                name: "track_id",
                table: "issued_ecf");
        }
    }
}
