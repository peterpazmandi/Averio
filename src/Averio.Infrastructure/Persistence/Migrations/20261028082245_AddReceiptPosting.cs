using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_status",
                table: "receipt_scans");

            migrationBuilder.AddColumn<Guid>(
                name: "posted_transaction_id",
                table: "receipt_scans",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_receipt_scans_household_id_posted_transaction_id",
                table: "receipt_scans",
                columns: new[] { "household_id", "posted_transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_scans_posted_transaction_id",
                table: "receipt_scans",
                column: "posted_transaction_id",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_posted",
                table: "receipt_scans",
                sql: "(status = 'Posted') = (posted_transaction_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_status",
                table: "receipt_scans",
                sql: "status IN ('Uploading','Queued','Processing','Ready','Failed','Discarded','Posted')");

            migrationBuilder.AddForeignKey(
                name: "FK_receipt_scans_transactions_household_id_posted_transaction_~",
                table: "receipt_scans",
                columns: new[] { "household_id", "posted_transaction_id" },
                principalTable: "transactions",
                principalColumns: new[] { "household_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_receipt_scans_transactions_household_id_posted_transaction_~",
                table: "receipt_scans");

            migrationBuilder.DropIndex(
                name: "IX_receipt_scans_household_id_posted_transaction_id",
                table: "receipt_scans");

            migrationBuilder.DropIndex(
                name: "IX_receipt_scans_posted_transaction_id",
                table: "receipt_scans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_posted",
                table: "receipt_scans");

            migrationBuilder.DropCheckConstraint(
                name: "ck_receipt_status",
                table: "receipt_scans");

            migrationBuilder.DropColumn(
                name: "posted_transaction_id",
                table: "receipt_scans");

            migrationBuilder.AddCheckConstraint(
                name: "ck_receipt_status",
                table: "receipt_scans",
                sql: "status IN ('Uploading','Queued','Processing','Ready','Failed','Discarded')");
        }
    }
}
