using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TransactionImportOrigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "import_batch_id",
                table: "transactions",
                type: "uuid",
                nullable: true);

            // Recover origin only from exact persisted links; preserve financial revisions.
            migrationBuilder.Sql("""
                UPDATE transactions AS t
                SET import_batch_id = b.id
                FROM import_batches AS b,
                     jsonb_array_elements(COALESCE(b.record_links, '[]'::jsonb)) AS link
                WHERE b.completed_at IS NOT NULL
                  AND t.household_id = b.household_id
                  AND t.created_by_household_member_id = b.member_id
                  AND link->>'Kind' <> 'Transfer'
                  AND t.id::text = link->>'Id';
                """);
            migrationBuilder.AddUniqueConstraint(
                name: "AK_import_batches_household_id_id",
                table: "import_batches",
                columns: new[] { "household_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_household_id_import_batch_id",
                table: "transactions",
                columns: new[] { "household_id", "import_batch_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_import_batches_household_id_import_batch_id",
                table: "transactions",
                columns: new[] { "household_id", "import_batch_id" },
                principalTable: "import_batches",
                principalColumns: new[] { "household_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_import_batches_household_id_import_batch_id",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "IX_transactions_household_id_import_batch_id",
                table: "transactions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_import_batches_household_id_id",
                table: "import_batches");

            migrationBuilder.DropColumn(
                name: "import_batch_id",
                table: "transactions");
        }
    }
}
