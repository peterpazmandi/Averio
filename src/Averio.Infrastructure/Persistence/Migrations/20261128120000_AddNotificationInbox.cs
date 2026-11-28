using System;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Averio.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AverioDbContext))]
[Migration("20261128120000_AddNotificationInbox")]
public sealed class AddNotificationInbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(name: "user_notifications", columns: table => new
        {
            id = table.Column<Guid>("uuid", nullable: false),
            recipient_user_id = table.Column<Guid>("uuid", nullable: false),
            household_id = table.Column<Guid>("uuid", nullable: false),
            household_member_id = table.Column<Guid>("uuid", nullable: false),
            type = table.Column<string>("character varying(48)", maxLength: 48, nullable: false),
            target_id = table.Column<Guid>("uuid", nullable: false),
            occurrence_key = table.Column<string>("character varying(80)", maxLength: 80, nullable: false),
            created_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: false),
            read_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
            dismissed_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true),
            resolved_at = table.Column<DateTimeOffset>("timestamp with time zone", nullable: true)
        }, constraints: table =>
        {
            table.PrimaryKey("pk_user_notifications", x => x.id);
            table.ForeignKey("fk_user_notifications_users_recipient_user_id", x => x.recipient_user_id,
                "users", "id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("fk_user_notifications_household_members_household_id_household_member_id",
                x => new { x.household_id, x.household_member_id }, "household_members",
                new[] { "household_id", "id" }, onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("ux_user_notifications_recipient_event",
            "user_notifications", new[] { "recipient_user_id", "type", "target_id", "occurrence_key" }, unique: true);
        migrationBuilder.CreateIndex("ix_user_notifications_recipient_user_id_created_at_id",
            "user_notifications", new[] { "recipient_user_id", "created_at", "id" });
        migrationBuilder.CreateIndex("ix_user_notifications_household_id_household_member_id",
            "user_notifications", new[] { "household_id", "household_member_id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("user_notifications");
}
