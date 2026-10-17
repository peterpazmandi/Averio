using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyRecurringFrequency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_recurring_frequency",
                table: "recurring_rules");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurring_frequency",
                table: "recurring_rules",
                sql: "frequency IN ('Daily', 'Weekly', 'Monthly', 'Yearly')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_recurring_frequency",
                table: "recurring_rules");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recurring_frequency",
                table: "recurring_rules",
                sql: "frequency IN ('Weekly', 'Monthly', 'Yearly')");
        }
    }
}
