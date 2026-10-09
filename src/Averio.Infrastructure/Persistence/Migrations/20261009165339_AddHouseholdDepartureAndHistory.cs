using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdDepartureAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_household_members_household_owner",
                table: "household_members");

            migrationBuilder.DropIndex(
                name: "ux_household_members_user_id",
                table: "household_members");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "archived_at_utc",
                table: "households",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "left_at_utc",
                table: "household_members",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "cutover_at_utc",
                table: "accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "household_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    left_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    was_last_member = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_household_history_households_household_id",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_household_history_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_household_members_household_owner",
                table: "household_members",
                column: "household_id",
                unique: true,
                filter: "role = 'owner' AND left_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_household_members_user_id",
                table: "household_members",
                column: "user_id",
                unique: true,
                filter: "left_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_household_history_household_id",
                table: "household_history",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_household_history_user_id_left_at_utc",
                table: "household_history",
                columns: new[] { "user_id", "left_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "household_history");

            migrationBuilder.DropIndex(
                name: "ux_household_members_household_owner",
                table: "household_members");

            migrationBuilder.DropIndex(
                name: "ux_household_members_user_id",
                table: "household_members");

            migrationBuilder.DropColumn(
                name: "archived_at_utc",
                table: "households");

            migrationBuilder.DropColumn(
                name: "left_at_utc",
                table: "household_members");

            migrationBuilder.DropColumn(
                name: "cutover_at_utc",
                table: "accounts");

            migrationBuilder.CreateIndex(
                name: "ux_household_members_household_owner",
                table: "household_members",
                column: "household_id",
                unique: true,
                filter: "role = 'owner'");

            migrationBuilder.CreateIndex(
                name: "ux_household_members_user_id",
                table: "household_members",
                column: "user_id",
                unique: true);
        }
    }
}
