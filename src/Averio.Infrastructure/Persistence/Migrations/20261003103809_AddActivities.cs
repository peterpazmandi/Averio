using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddActivities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    starts_on = table.Column<DateOnly>(type: "date", nullable: true),
                    ends_on = table.Column<DateOnly>(type: "date", nullable: true),
                    is_closed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.id);
                    table.UniqueConstraint("AK_activities_household_id_household_member_id_id", x => new { x.household_id, x.household_member_id, x.id });
                    table.CheckConstraint("ck_activities_dates", "ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on");
                    table.CheckConstraint("ck_activities_name", "length(btrim(name)) > 0");
                    table.ForeignKey(
                        name: "FK_activities_household_members_household_id_household_member_~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "activity_assignments",
                columns: table => new
                {
                    household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activity_assignments", x => new { x.household_member_id, x.transaction_id });
                    table.ForeignKey(
                        name: "FK_activity_assignments_activities_household_id_household_memb~",
                        columns: x => new { x.household_id, x.household_member_id, x.activity_id },
                        principalTable: "activities",
                        principalColumns: new[] { "household_id", "household_member_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_assignments_household_members_household_id_househo~",
                        columns: x => new { x.household_id, x.household_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_activity_assignments_transactions_household_id_transaction_~",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_household_id_household_member_id_is_closed_name",
                table: "activities",
                columns: new[] { "household_id", "household_member_id", "is_closed", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_assignments_household_id_household_member_id_activ~",
                table: "activity_assignments",
                columns: new[] { "household_id", "household_member_id", "activity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_activity_assignments_household_id_transaction_id",
                table: "activity_assignments",
                columns: new[] { "household_id", "transaction_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_assignments");

            migrationBuilder.DropTable(
                name: "activities");
        }
    }
}
