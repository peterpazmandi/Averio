using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveRefundCreditOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_item_refunds_credit_transaction_id",
                table: "item_refunds");

            migrationBuilder.AddColumn<string>(
                name: "credit_origin",
                table: "item_refunds",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "original_income_json",
                table: "item_refunds",
                type: "jsonb",
                nullable: true);

            // Historical records did not retain origin or replaced items. Never infer from timestamps.
            migrationBuilder.Sql("ALTER TABLE item_refunds ALTER COLUMN credit_origin DROP DEFAULT");
            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_credit_transaction_id",
                table: "item_refunds",
                column: "credit_transaction_id",
                unique: true,
                filter: "cancelled_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_item_refunds_origin",
                table: "item_refunds",
                sql: "(credit_origin IN ('Unknown', 'Created') AND original_income_json IS NULL) OR (credit_origin = 'ExistingIncome' AND original_income_json IS NOT NULL AND jsonb_typeof(original_income_json) = 'object')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM item_refunds WHERE credit_origin <> 'Unknown') THEN
                        RAISE EXCEPTION 'Cannot discard refund provenance. Restore a pre-upgrade backup instead.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_item_refunds_credit_transaction_id",
                table: "item_refunds");

            migrationBuilder.DropCheckConstraint(
                name: "ck_item_refunds_origin",
                table: "item_refunds");

            migrationBuilder.DropColumn(
                name: "credit_origin",
                table: "item_refunds");

            migrationBuilder.DropColumn(
                name: "original_income_json",
                table: "item_refunds");

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_credit_transaction_id",
                table: "item_refunds",
                column: "credit_transaction_id",
                unique: true);
        }
    }
}
