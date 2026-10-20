using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Averio.Infrastructure.Persistence;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AverioDbContext))]
[Migration("20261019220000_AddPersonalMoneyReconciliation")]
public sealed class AddPersonalMoneyReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_personal_money_entry_kind", table: "personal_money_entries");
        migrationBuilder.DropCheckConstraint(
            name: "ck_personal_money_entry_source", table: "personal_money_entries");

        migrationBuilder.AddColumn<Guid>(
            name: "related_transaction_id", table: "personal_money_entries",
            type: "uuid", nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_personal_money_entries_household_id_related_transaction_id",
            table: "personal_money_entries",
            columns: new[] { "household_id", "related_transaction_id" });

        migrationBuilder.AddForeignKey(
            name: "FK_personal_money_entries_transactions_household_id_related_transaction_id",
            table: "personal_money_entries",
            columns: new[] { "household_id", "related_transaction_id" },
            principalTable: "transactions", principalColumns: new[] { "household_id", "id" },
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddCheckConstraint(
            name: "ck_personal_money_entry_kind", table: "personal_money_entries",
            sql: "kind IN ('Grant','Funding','PrivateCharge','SharedCharge','PrivateRefund','SharedRefund','Surrender')");
        migrationBuilder.AddCheckConstraint(
            name: "ck_personal_money_entry_source", table: "personal_money_entries",
            sql: "(transfer_id IS NULL OR transaction_id IS NULL) AND " +
                 "(kind <> 'Funding' OR transfer_id IS NOT NULL) AND " +
                 "(kind NOT IN ('PrivateCharge','SharedCharge','PrivateRefund','SharedRefund') OR transaction_id IS NOT NULL) AND " +
                 "(kind NOT IN ('PrivateRefund','SharedRefund') OR related_transaction_id IS NOT NULL) AND " +
                 "(related_transaction_id IS NULL OR transaction_id IS NOT NULL)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint("ck_personal_money_entry_kind", "personal_money_entries");
        migrationBuilder.DropCheckConstraint("ck_personal_money_entry_source", "personal_money_entries");
        migrationBuilder.DropForeignKey(
            "FK_personal_money_entries_transactions_household_id_related_transaction_id",
            "personal_money_entries");
        migrationBuilder.DropIndex(
            "IX_personal_money_entries_household_id_related_transaction_id",
            "personal_money_entries");
        migrationBuilder.DropColumn("related_transaction_id", "personal_money_entries");
        migrationBuilder.AddCheckConstraint(
            "ck_personal_money_entry_kind", "personal_money_entries",
            "kind IN ('Grant','Funding','PrivateCharge')");
        migrationBuilder.AddCheckConstraint(
            "ck_personal_money_entry_source", "personal_money_entries",
            "(transfer_id IS NULL OR transaction_id IS NULL) AND " +
            "(kind <> 'Funding' OR transfer_id IS NOT NULL) AND " +
            "(kind <> 'PrivateCharge' OR transaction_id IS NOT NULL)");
    }
}
