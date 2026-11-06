using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportHistoryTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "record_links",
                table: "import_batches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_name",
                table: "import_batches",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "summary",
                table: "import_batches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_import_batches_household_id_member_id_completed_at_id",
                table: "import_batches",
                columns: new[] { "household_id", "member_id", "completed_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_import_batches_household_id_member_id_completed_at_id",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "record_links",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "source_name",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "summary",
                table: "import_batches");
        }
    }
}
