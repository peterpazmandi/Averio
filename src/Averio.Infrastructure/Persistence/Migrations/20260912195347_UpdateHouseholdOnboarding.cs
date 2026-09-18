using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateHouseholdOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "financial_setup_completed_at_utc",
                table: "households",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_household_members_household_id_id",
                table: "household_members",
                columns: new[] { "household_id", "id" });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_household_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ownership = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    visibility = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    include_in_household_reports = table.Column<bool>(type: "boolean", nullable: false),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    balance_anchor_amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false),
                    balance_anchor_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.id);
                    table.CheckConstraint("ck_accounts_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
                    table.CheckConstraint("ck_accounts_ownership_visibility", "(\r\n    (\r\n        ownership = 'Household'\r\n        AND owner_household_member_id IS NULL\r\n        AND visibility = 'Household'\r\n        AND include_in_household_reports = true\r\n    )\r\n    OR\r\n    (\r\n        ownership = 'Personal'\r\n        AND owner_household_member_id IS NOT NULL\r\n        AND (\r\n            visibility <> 'Private'\r\n            OR include_in_household_reports = false\r\n        )\r\n    )\r\n)");
                    table.ForeignKey(
                        name: "FK_accounts_household_members_household_id_owner_household_mem~",
                        columns: x => new { x.household_id, x.owner_household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id",
                table: "accounts",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id_owner_household_member_id",
                table: "accounts",
                columns: new[] { "household_id", "owner_household_member_id" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id_status",
                table: "accounts",
                columns: new[] { "household_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_household_id_visibility",
                table: "accounts",
                columns: new[] { "household_id", "visibility" });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_owner_household_member_id",
                table: "accounts",
                column: "owner_household_member_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_household_members_household_id_id",
                table: "household_members");

            migrationBuilder.DropColumn(
                name: "financial_setup_completed_at_utc",
                table: "households");
        }
    }
}
