using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendRecurringTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "activity_id",
                table: "recurring_rules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant",
                table: "recurring_rules",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "note",
                table: "recurring_rules",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recurring_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_items", x => x.id);
                    table.CheckConstraint("ck_recurring_item_amount", "amount > 0");
                    table.CheckConstraint("ck_recurring_item_description", "length(btrim(description)) > 0 AND position >= 0");
                    table.ForeignKey(
                        name: "FK_recurring_items_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_items_recurring_rules_rule_id",
                        column: x => x.rule_id,
                        principalTable: "recurring_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_labels",
                columns: table => new
                {
                    rule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recurring_labels", x => new { x.rule_id, x.label_id });
                    table.ForeignKey(
                        name: "FK_recurring_labels_labels_household_id_label_id",
                        columns: x => new { x.household_id, x.label_id },
                        principalTable: "labels",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_recurring_labels_recurring_rules_household_id_rule_id",
                        columns: x => new { x.household_id, x.rule_id },
                        principalTable: "recurring_rules",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_rules_household_id_household_member_id_activity_id",
                table: "recurring_rules",
                columns: new[] { "household_id", "household_member_id", "activity_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_items_category_id",
                table: "recurring_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_recurring_items_rule_id_position",
                table: "recurring_items",
                columns: new[] { "rule_id", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_labels_household_id_label_id",
                table: "recurring_labels",
                columns: new[] { "household_id", "label_id" });

            migrationBuilder.CreateIndex(
                name: "IX_recurring_labels_household_id_rule_id",
                table: "recurring_labels",
                columns: new[] { "household_id", "rule_id" });

            migrationBuilder.AddForeignKey(
                name: "FK_recurring_rules_activities_household_id_household_member_id~",
                table: "recurring_rules",
                columns: new[] { "household_id", "household_member_id", "activity_id" },
                principalTable: "activities",
                principalColumns: new[] { "household_id", "household_member_id", "id" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                INSERT INTO recurring_items (id, rule_id, position, description, category_id, amount)
                SELECT gen_random_uuid(), id, 0, name, category_id, amount FROM recurring_rules;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_recurring_rules_activities_household_id_household_member_id~",
                table: "recurring_rules");

            migrationBuilder.DropTable(
                name: "recurring_items");

            migrationBuilder.DropTable(
                name: "recurring_labels");

            migrationBuilder.DropIndex(
                name: "IX_recurring_rules_household_id_household_member_id_activity_id",
                table: "recurring_rules");

            migrationBuilder.DropColumn(
                name: "activity_id",
                table: "recurring_rules");

            migrationBuilder.DropColumn(
                name: "merchant",
                table: "recurring_rules");

            migrationBuilder.DropColumn(
                name: "note",
                table: "recurring_rules");
        }
    }
}
