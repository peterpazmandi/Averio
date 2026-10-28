using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptConfirmationFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "confirmation_fingerprint",
                table: "receipt_scans",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_confirmation_fingerprint",
                table: "receipt_scans",
                sql: "confirmation_fingerprint IS NULL OR (status = 'Posted' AND confirmation_fingerprint ~ '^[0-9a-f]{64}$')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_confirmation_fingerprint",
                table: "receipt_scans");

            migrationBuilder.DropColumn(
                name: "confirmation_fingerprint",
                table: "receipt_scans");
        }
    }
}
