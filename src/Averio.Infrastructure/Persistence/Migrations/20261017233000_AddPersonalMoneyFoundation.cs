using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Averio.Infrastructure.Persistence;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AverioDbContext))]
[Migration("20261017233000_AddPersonalMoneyFoundation")]
public sealed class AddPersonalMoneyFoundation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "personal_money_wallets",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: false),
                household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_personal_money_wallets", x => x.id);
                table.UniqueConstraint("ak_personal_money_wallet_household_id_id", x => new { x.household_id, x.id });
                table.ForeignKey("FK_personal_money_wallets_household_members_household_id_household_member_id",
                    x => new { x.household_id, x.household_member_id },
                    "household_members", new[] { "household_id", "id" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_personal_money_wallet_currency", "currency IN ('EUR','USD','HUF')");
            });

        migrationBuilder.CreateTable(
            name: "personal_money_entries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: false),
                wallet_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                transfer_id = table.Column<Guid>(type: "uuid", nullable: true),
                transaction_id = table.Column<Guid>(type: "uuid", nullable: true),
                reverses_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_personal_money_entries", x => x.id);
                table.ForeignKey("FK_personal_money_entries_personal_money_wallets_household_id_wallet_id",
                    x => new { x.household_id, x.wallet_id }, "personal_money_wallets",
                    new[] { "household_id", "id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_personal_money_entries_transfers_transfer_id", x => x.transfer_id,
                    "transfers", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_personal_money_entries_transactions_household_id_transaction_id",
                    x => new { x.household_id, x.transaction_id }, "transactions",
                    new[] { "household_id", "id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_personal_money_entries_personal_money_entries_reverses_entry_id",
                    x => x.reverses_entry_id, "personal_money_entries", "id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_personal_money_entry_amount", "amount <> 0");
                table.CheckConstraint("ck_personal_money_entry_kind",
                    "kind IN ('Grant','Funding','PrivateCharge')");
                table.CheckConstraint("ck_personal_money_entry_source",
                    "(transfer_id IS NULL OR transaction_id IS NULL) AND " +
                    "(kind <> 'Funding' OR transfer_id IS NOT NULL) AND " +
                    "(kind <> 'PrivateCharge' OR transaction_id IS NOT NULL)");
            });

        migrationBuilder.CreateIndex("IX_personal_money_wallets_household_id_household_member_id",
            "personal_money_wallets", new[] { "household_id", "household_member_id" }, unique: true);
        migrationBuilder.CreateIndex("IX_personal_money_entries_wallet_id_occurred_on_recorded_at",
            "personal_money_entries", new[] { "wallet_id", "occurred_on", "recorded_at" });
        migrationBuilder.CreateIndex("IX_personal_money_entries_household_id_transfer_id",
            "personal_money_entries", new[] { "household_id", "transfer_id" });
        migrationBuilder.CreateIndex("IX_personal_money_entries_household_id_transaction_id",
            "personal_money_entries", new[] { "household_id", "transaction_id" });
        migrationBuilder.CreateIndex("IX_personal_money_entries_reverses_entry_id",
            "personal_money_entries", "reverses_entry_id", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "personal_money_entries");
        migrationBuilder.DropTable(name: "personal_money_wallets");
    }
}
