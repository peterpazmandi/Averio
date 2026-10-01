using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_categories_not_self_parent",
                table: "categories",
                sql: "parent_id IS NULL OR parent_id <> id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_categories_not_self_parent",
                table: "categories");
        }
    }
}
