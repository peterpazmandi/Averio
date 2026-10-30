using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptCategoryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_category_rules",
                columns: table => new
                {
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_receipt_category_rules", x => new { x.household_id, x.match_key });
                    table.CheckConstraint("ck_receipt_category_rule_key", "match_key ~ '^[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_receipt_category_rules_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_receipt_category_rules_household_members_household_id_updat~",
                        columns: x => new { x.household_id, x.updated_by_member_id },
                        principalTable: "household_members",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_receipt_category_rules_category_id",
                table: "receipt_category_rules",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_receipt_category_rules_household_id_updated_by_member_id",
                table: "receipt_category_rules",
                columns: new[] { "household_id", "updated_by_member_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_category_rules");
        }
    }
}
