using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transfers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    destination_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    sent_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    received_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    occurred_on = table.Column<DateOnly>(type: "date", nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    voided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transfers", x => x.id);
                    table.CheckConstraint("ck_transfers_amounts", "sent_amount > 0 AND received_amount > 0");
                    table.CheckConstraint("ck_transfers_currencies", "source_currency IN ('EUR','USD','HUF') AND destination_currency IN ('EUR','USD','HUF')");
                    table.CheckConstraint("ck_transfers_distinct_accounts", "source_account_id <> destination_account_id");
                    table.CheckConstraint("ck_transfers_same_currency", "source_currency <> destination_currency OR sent_amount = received_amount");
                    table.ForeignKey(
                        name: "FK_transfers_accounts_household_id_destination_account_id",
                        columns: x => new { x.household_id, x.destination_account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_accounts_household_id_source_account_id",
                        columns: x => new { x.household_id, x.source_account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_transfers_household_members_household_id_created_by_househo~",
                        columns: x => new { x.household_id, x.created_by_household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_created_by_household_member_id",
                table: "transfers",
                columns: new[] { "household_id", "created_by_household_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_destination_account_id_occurred_on",
                table: "transfers",
                columns: new[] { "household_id", "destination_account_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_occurred_on_created_at_id",
                table: "transfers",
                columns: new[] { "household_id", "occurred_on", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_transfers_household_id_source_account_id_occurred_on",
                table: "transfers",
                columns: new[] { "household_id", "source_account_id", "occurred_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transfers");
        }
    }
}
