using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recurring_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    frequency = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false),
                    next_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    automatic = table.Column<bool>(type: "boolean", nullable: false),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    is_stopped = table.Column<bool>(type: "boolean", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_rules", x => x.id);
                    table.UniqueConstraint("AK_recurring_rules_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_recurring_amount", "amount > 0");
                    table.CheckConstraint("ck_recurring_dates", "(ends_on IS NULL OR ends_on >= starts_on) AND occurrence_index >= 0 AND (next_due_on IS NULL OR next_due_on >= starts_on)");
                    table.CheckConstraint("ck_recurring_frequency", "frequency IN ('Weekly', 'Monthly', 'Yearly')");
                    table.CheckConstraint("ck_recurring_kind", "kind IN ('Income', 'Expense')");
                    table.CheckConstraint("ck_recurring_name", "length(btrim(name)) > 0");
                    table.CheckConstraint("ck_recurring_visibility", "visibility IN ('Household', 'Private')");
                    table.ForeignKey(
                        name: "FK_recurring_rules_accounts_household_id_account_id",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_rules_household_members_household_id_household_me~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recurring_occurrences",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_on = table.Column<DateOnly>(type: "date", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_occurrences", x => new { x.rule_id, x.due_on });
                    table.CheckConstraint("ck_recurring_receipt", "(transaction_id IS NULL AND amount IS NULL) OR (transaction_id IS NOT NULL AND amount IS NOT NULL AND amount > 0)");
                    table.ForeignKey(
                        name: "FK_recurring_occurrences_recurring_rules_household_id_rule_id",
                        columns: x => new { x.household_id, x.rule_id },
                        principalTable: "recurring_rules",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_occurrences_transactions_household_id_transaction~",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_household_id_rule_id",
                table: "recurring_occurrences",
                columns: new[] { "household_id", "rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_household_id_transaction_id",
                table: "recurring_occurrences",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_occurrences_transaction_id",
                table: "recurring_occurrences",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_category_id",
                table: "recurring_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_household_id_account_id",
                table: "recurring_rules",
                columns: new[] { "household_id", "account_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_household_id_household_member_id",
                table: "recurring_rules",
                columns: new[] { "household_id", "household_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_next_due_on_id",
                table: "recurring_rules",
                columns: new[] { "next_due_on", "id" },
                filter: "automatic AND NOT is_paused AND NOT is_stopped AND NOT is_blocked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recurring_occurrences");

            migrationBuilder.DropTable(
                name: "recurring_rules");
        }
    }
}
