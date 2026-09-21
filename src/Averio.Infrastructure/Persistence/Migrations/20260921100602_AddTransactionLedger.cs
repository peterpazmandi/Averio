using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transaction_aggregates",
                columns: table => new
                {
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    period = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    income_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    expense_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    transaction_count = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_aggregates", x => new { x.household_id, x.account_id, x.currency_code, x.period, x.period_start });
                    table.CheckConstraint("ck_transaction_aggregates_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
                    table.CheckConstraint("ck_transaction_aggregates_non_negative", "income_amount >= 0 AND expense_amount >= 0 AND transaction_count >= 0");
                    table.CheckConstraint("ck_transaction_aggregates_period", "period IN ('Day', 'Month')");
                    table.ForeignKey(
                        name: "fk_transaction_aggregates_accounts_household_account",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.id);
                    table.CheckConstraint("ck_transactions_amount", "amount > 0");
                    table.CheckConstraint("ck_transactions_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
                    table.CheckConstraint("ck_transactions_kind", "kind IN ('Income', 'Expense')");
                    table.CheckConstraint("ck_transactions_status", "status IN ('Posted', 'Voided')");
                    table.CheckConstraint("ck_transactions_visibility", "visibility IN ('Household', 'Private')");
                    table.ForeignKey(
                        name: "fk_transactions_accounts_household_account",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transactions_household_members_creator",
                        columns: x => new { x.household_id, x.created_by_household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_aggregates_household_period",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "period", "period_start", "currency_code" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_account_date",
                table: "transactions",
                columns: new[] { "household_id", "account_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_date_created",
                table: "transactions",
                columns: new[] { "household_id", "occurred_on", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_household_id_created_by_household_member_id",
                table: "transactions",
                columns: new[] { "household_id", "created_by_household_member_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_status_date",
                table: "transactions",
                columns: new[] { "household_id", "status", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_household_visibility_date",
                table: "transactions",
                columns: new[] { "household_id", "visibility", "occurred_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction_aggregates");

            migrationBuilder.DropTable(
                name: "transactions");
        }
    }
}
