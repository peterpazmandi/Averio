using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryPinOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "category_pins",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                WITH ranked AS (
                    SELECT household_member_id, category_id,
                        row_number() OVER (PARTITION BY household_member_id ORDER BY created_at, category_id) - 1 AS position
                    FROM category_pins
                )
                UPDATE category_pins AS pin SET sort_order = ranked.position
                FROM ranked WHERE pin.household_member_id = ranked.household_member_id AND pin.category_id = ranked.category_id;
                """);
            migrationBuilder.AddCheckConstraint(
                name: "ck_category_pins_sort_order",
                table: "category_pins",
                sql: "sort_order >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_category_pins_sort_order",
                table: "category_pins");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "category_pins");
        }
    }
}
