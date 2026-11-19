using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItemRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_amount",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_kind",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_items_amount",
                table: "transaction_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries");

            migrationBuilder.AddColumn<bool>(
                name: "has_refund_history",
                table: "transaction_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "refunded_amount",
                table: "transaction_items",
                type: "numeric(19,4)",
                precision: 19,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_transaction_items_household_id_transaction_id_id",
                table: "transaction_items",
                columns: new[] { "household_id", "transaction_id", "id" });

            migrationBuilder.CreateTable(
                name: "item_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    credit_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    received_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    request_fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_item_refunds", x => x.id);
                    table.CheckConstraint("ck_item_refunds_amount", "amount > 0");
                    table.CheckConstraint("ck_item_refunds_cancellation", "(cancelled_at IS NULL) = (cancelled_by_member_id IS NULL)");
                    table.CheckConstraint("ck_item_refunds_distinct", "transaction_id <> credit_transaction_id");
                    table.ForeignKey(
                        name: "FK_item_refunds_household_members_household_id_cancelled_by_me~",
                        columns: x => new { x.household_id, x.cancelled_by_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_refunds_household_members_household_id_created_by_memb~",
                        columns: x => new { x.household_id, x.created_by_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_refunds_transaction_items_household_id_transaction_id_~",
                        columns: x => new { x.household_id, x.transaction_id, x.transaction_item_id },
                        principalTable: "transaction_items",
                        principalColumns: new[] { "household_id", "transaction_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_item_refunds_transactions_household_id_credit_transaction_id",
                        columns: x => new { x.household_id, x.credit_transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_amount",
                table: "transactions",
                sql: "amount >= 0 AND (kind = 'Expense' OR amount > 0)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_kind",
                table: "transactions",
                sql: "kind IN ('Income', 'Expense', 'Refund')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_items_amount",
                table: "transaction_items",
                sql: "amount >= 0 AND refunded_amount >= 0 AND amount + refunded_amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries",
                sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL) AND (recurring_allowance_rule_id IS NULL OR kind = 'Grant') AND (transaction_item_id IS NULL OR (transaction_id IS NOT NULL AND kind IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund')))");

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_credit_transaction_id",
                table: "item_refunds",
                column: "credit_transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_household_id_cancelled_by_member_id",
                table: "item_refunds",
                columns: new[] { "household_id", "cancelled_by_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_household_id_created_by_member_id",
                table: "item_refunds",
                columns: new[] { "household_id", "created_by_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_household_id_credit_transaction_id",
                table: "item_refunds",
                columns: new[] { "household_id", "credit_transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_household_id_operation_id",
                table: "item_refunds",
                columns: new[] { "household_id", "operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_household_id_transaction_id",
                table: "item_refunds",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_item_refunds_household_id_transaction_id_transaction_item_id",
                table: "item_refunds",
                columns: new[] { "household_id", "transaction_id", "transaction_item_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $ BEGIN
                    IF EXISTS (SELECT 1 FROM item_refunds) OR EXISTS (SELECT 1 FROM transactions WHERE kind = 'Refund') THEN
                        RAISE EXCEPTION 'Item refunds exist. Preserve and resolve refund history before rolling back this migration.';
                    END IF;
                END $;
                """);
            migrationBuilder.DropTable(
                name: "item_refunds");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_amount",
                table: "transactions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transactions_kind",
                table: "transactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_transaction_items_household_id_transaction_id_id",
                table: "transaction_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_items_amount",
                table: "transaction_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries");

            migrationBuilder.DropColumn(
                name: "has_refund_history",
                table: "transaction_items");

            migrationBuilder.DropColumn(
                name: "refunded_amount",
                table: "transaction_items");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_amount",
                table: "transactions",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transactions_kind",
                table: "transactions",
                sql: "kind IN ('Income', 'Expense')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_items_amount",
                table: "transaction_items",
                sql: "amount > 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries",
                sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL) AND (recurring_allowance_rule_id IS NULL OR kind = 'Grant') AND (transaction_item_id IS NULL OR (transaction_id IS NOT NULL AND kind IN ('PrivateCharge','SharedCharge')))");
        }
    }
}
