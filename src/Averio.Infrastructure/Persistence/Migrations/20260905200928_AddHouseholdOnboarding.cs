using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseholdOnboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "display_name",
                table: "users",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                table: "households",
                type: "char(2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "onboarding_completed_at_utc",
                table: "households",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "preferred_currency",
                table: "households",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_households_country_code",
                table: "households",
                sql: "country_code IS NULL\r\nOR country_code ~ '^[A-Z]{2}$'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_households_preferred_currency",
                table: "households",
                sql: "preferred_currency IS NULL\r\nOR preferred_currency IN ('HUF', 'EUR', 'USD')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_households_country_code",
                table: "households");

            migrationBuilder.DropCheckConstraint(
                name: "ck_households_preferred_currency",
                table: "households");

            migrationBuilder.DropColumn(
                name: "display_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "country_code",
                table: "households");

            migrationBuilder.DropColumn(
                name: "onboarding_completed_at_utc",
                table: "households");

            migrationBuilder.DropColumn(
                name: "preferred_currency",
                table: "households");
        }
    }
}
