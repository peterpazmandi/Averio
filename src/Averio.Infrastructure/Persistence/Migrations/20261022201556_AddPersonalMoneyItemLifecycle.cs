using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersonalMoneyItemLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries");

            migrationBuilder.AddColumn<bool>(
                name: "is_archived",
                table: "transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "transaction_item_id",
                table: "personal_money_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_personal_money_entries_household_id_transaction_id_transact~",
                table: "personal_money_entries",
                columns: new[] { "household_id", "transaction_id", "transaction_item_id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries",
                sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL) AND (recurring_allowance_rule_id IS NULL OR kind = 'Grant') AND (transaction_item_id IS NULL OR (transaction_id IS NOT NULL AND kind IN ('PrivateCharge','SharedCharge')))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_personal_money_entries_household_id_transaction_id_transact~",
                table: "personal_money_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries");

            migrationBuilder.DropColumn(
                name: "is_archived",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "transaction_item_id",
                table: "personal_money_entries");

            migrationBuilder.AddCheckConstraint(
                name: "ck_personal_money_entry_source",
                table: "personal_money_entries",
                sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND (kind <> 'Funding' OR transfer_id IS NOT NULL) AND (kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND (kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND (related_transaction_id IS NULL OR transaction_id IS NOT NULL) AND (recurring_allowance_rule_id IS NULL OR kind = 'Grant')");
        }
    }
}
