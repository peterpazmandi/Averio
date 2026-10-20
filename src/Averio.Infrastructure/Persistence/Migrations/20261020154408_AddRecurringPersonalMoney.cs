using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringPersonalMoney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries");

            migrationBuilder.AddColumn<Guid>(
                name: "recurring_allowance_rule_id",
                table: "personal_money_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recurring_allowance_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    creation_operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    creation_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    creation_ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: false),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    occurrence_index = table.Column<int>(type: "integer", nullable: false),
                    next_due_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_paused = table.Column<bool>(type: "boolean", nullable: false),
                    is_stopped = table.Column<bool>(type: "boolean", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_allowance_rules", x => x.id);
                    table.UniqueConstraint("AK_recurring_allowance_rules_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_recurring_allowance_amount", "amount > 0");
                    table.CheckConstraint("ck_recurring_allowance_currency", "currency IN ('EUR','USD','HUF')");
                    table.CheckConstraint("ck_recurring_allowance_dates", "(ends_on IS NULL OR ends_on >= starts_on) AND occurrence_index >= 0 AND (next_due_on IS NULL OR next_due_on >= starts_on)");
                    table.ForeignKey(
                        name: "FK_recurring_allowance_rules_household_members_household_id_re~",
                        columns: x => new { x.household_id, x.recipient_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_recurring_allowance_rul~",
                table: "personal_money_entries",
                columns: new[] { "household_id", "recurring_allowance_rule_id" });

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_recurring_allowance_rule_id_occurred~",
                table: "personal_money_entries",
                columns: new[] { "recurring_allowance_rule_id", "occurred_on" },
                unique: true,
                filter: "recurring_allowance_rule_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries",
                sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL) AND (recurring_allowance_rule_id IS NULL OR kind = 'Grant')");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_allowance_rules_household_id_creation_operation_id",
                table: "recurring_allowance_rules",
                columns: new[] { "household_id", "creation_operation_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recurring_allowance_rules_household_id_recipient_member_id",
                table: "recurring_allowance_rules",
                columns: new[] { "household_id", "recipient_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_allowance_rules_next_due_on_id",
                table: "recurring_allowance_rules",
                columns: new[] { "next_due_on", "id" },
                filter: "NOT is_paused AND NOT is_stopped AND NOT is_blocked AND next_due_on IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_personal_money_entries_recurring_allowance_rules_household_~",
                table: "personal_money_entries",
                columns: new[] { "household_id", "recurring_allowance_rule_id" },
                principalTable: "recurring_allowance_rules",
                principalColumns: new[] { "household_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_personal_money_entries_recurring_allowance_rules_household_~",
                table: "personal_money_entries");

            migrationBuilder.DropTable(
                name: "recurring_allowance_rules");

            migrationBuilder.DropIndex(
                name: "IX_personal_money_entries_household_id_recurring_allowance_rul~",
                table: "personal_money_entries");

            migrationBuilder.DropIndex(
                name: "IX_personal_money_entries_recurring_allowance_rule_id_occurred~",
                table: "personal_money_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries");

            migrationBuilder.DropColumn(
                name: "recurring_allowance_rule_id",
                table: "personal_money_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries",
                sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL)");
        }
    }
}
