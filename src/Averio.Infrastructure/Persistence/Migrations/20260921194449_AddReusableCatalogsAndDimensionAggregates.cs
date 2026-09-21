using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReusableCatalogsAndDimensionAggregates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_transaction_aggregates",
                table: "transaction_aggregates");

            migrationBuilder.DropIndex(
                name: "ix_transaction_aggregates_household_period",
                table: "transaction_aggregates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_aggregates_non_negative",
                table: "transaction_aggregates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_aggregates_period",
                table: "transaction_aggregates");

            migrationBuilder.RenameColumn(
                name: "transaction_count",
                table: "transaction_aggregates",
                newName: "entry_count");

            migrationBuilder.AddColumn<string>(
                name: "merchant",
                table: "transactions",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dimension_type",
                table: "transaction_aggregates",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "dimension_id",
                table: "transaction_aggregates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "ak_transactions_household_id_id",
                table: "transactions",
                columns: new[] { "household_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_transaction_aggregates",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "account_id", "dimension_type", "dimension_id", "currency_code", "period", "period_start" });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    code = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    accent_color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_base = table.Column<bool>(type: "boolean", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.CheckConstraint("ck_categories_accent_color", "accent_color ~ '^#[0-9A-Fa-f]{6}$'");
                    table.CheckConstraint("ck_categories_kind", "kind IN ('Expense', 'Income', 'Both')");
                    table.CheckConstraint("ck_categories_scope", "(is_base AND household_id IS NULL) OR (NOT is_base AND household_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_categories_households",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_categories_parent",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "icons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_icons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_labels", x => x.id);
                    table.UniqueConstraint("ak_labels_household_id_id", x => new { x.household_id, x.id });
                    table.CheckConstraint("ck_labels_scope", "scope IN ('Household', 'Account')");
                    table.CheckConstraint("ck_labels_scope_account", "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_labels_accounts",
                        columns: x => new { x.household_id, x.account_id },
                        principalTable: "accounts",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_labels_households",
                        column: x => x.household_id,
                        principalTable: "households",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category_icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_items", x => x.id);
                    table.CheckConstraint("ck_transaction_items_amount", "amount > 0");
                    table.CheckConstraint("ck_transaction_items_position", "position >= 0");
                    table.ForeignKey(
                        name: "fk_transaction_items_categories",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_items_transactions",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transaction_labels",
                columns: table => new
                {
                    transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transaction_labels", x => new { x.transaction_id, x.label_id });
                    table.ForeignKey(
                        name: "fk_transaction_labels_labels",
                        columns: x => new { x.household_id, x.label_id },
                        principalTable: "labels",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_transaction_labels_transactions",
                        columns: x => new { x.household_id, x.transaction_id },
                        principalTable: "transactions",
                        principalColumns: new[] { "household_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_aggregates_dimension",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "dimension_type", "dimension_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_aggregates_household_dimension_period",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "dimension_type", "period", "period_start", "currency_code" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_aggregates_dimension",
                table: "transaction_aggregates",
                sql: "dimension_type IN ('Account', 'Category')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_aggregates_non_negative",
                table: "transaction_aggregates",
                sql: "income_amount >= 0 AND expense_amount >= 0 AND entry_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_aggregates_period",
                table: "transaction_aggregates",
                sql: "period IN ('Day', 'Month', 'Year')");

            migrationBuilder.CreateIndex(
                name: "ix_categories_base_kind_status",
                table: "categories",
                columns: new[] { "is_base", "kind", "is_archived" });

            migrationBuilder.CreateIndex(
                name: "ix_categories_household_parent_order",
                table: "categories",
                columns: new[] { "household_id", "parent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_categories_base_code",
                table: "categories",
                column: "code",
                unique: true,
                filter: "is_base");

            migrationBuilder.CreateIndex(
                name: "ix_icons_sort_order",
                table: "icons",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ux_icons_value",
                table: "icons",
                column: "value",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_labels_scope_account",
                table: "labels",
                columns: new[] { "household_id", "scope", "account_id" });

            migrationBuilder.CreateIndex(
                name: "ux_labels_account_name",
                table: "labels",
                columns: new[] { "household_id", "account_id", "normalized_name" },
                unique: true,
                filter: "scope = 'Account'");

            migrationBuilder.CreateIndex(
                name: "ux_labels_household_name",
                table: "labels",
                columns: new[] { "household_id", "normalized_name" },
                unique: true,
                filter: "scope = 'Household'");

            migrationBuilder.CreateIndex(
                name: "ix_transaction_items_category",
                table: "transaction_items",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_transaction_items_transaction_position",
                table: "transaction_items",
                columns: new[] { "household_id", "transaction_id", "position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_transaction_labels_household_id_transaction_id",
                table: "transaction_labels",
                columns: new[] { "household_id", "transaction_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_labels_label",
                table: "transaction_labels",
                columns: new[] { "household_id", "label_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "icons");

            migrationBuilder.DropTable(
                name: "transaction_items");

            migrationBuilder.DropTable(
                name: "transaction_labels");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "labels");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_transactions_household_id_id",
                table: "transactions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_transaction_aggregates",
                table: "transaction_aggregates");

            migrationBuilder.DropIndex(
                name: "ix_transaction_aggregates_dimension",
                table: "transaction_aggregates");

            migrationBuilder.DropIndex(
                name: "ix_transaction_aggregates_household_dimension_period",
                table: "transaction_aggregates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_aggregates_dimension",
                table: "transaction_aggregates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_aggregates_non_negative",
                table: "transaction_aggregates");

            migrationBuilder.DropCheckConstraint(
                name: "ck_transaction_aggregates_period",
                table: "transaction_aggregates");

            migrationBuilder.DropColumn(
                name: "merchant",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "dimension_type",
                table: "transaction_aggregates");

            migrationBuilder.DropColumn(
                name: "dimension_id",
                table: "transaction_aggregates");

            migrationBuilder.RenameColumn(
                name: "entry_count",
                table: "transaction_aggregates",
                newName: "transaction_count");

            migrationBuilder.AddPrimaryKey(
                name: "PK_transaction_aggregates",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "account_id", "currency_code", "period", "period_start" });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_aggregates_household_period",
                table: "transaction_aggregates",
                columns: new[] { "household_id", "period", "period_start", "currency_code" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_aggregates_non_negative",
                table: "transaction_aggregates",
                sql: "income_amount >= 0 AND expense_amount >= 0 AND transaction_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_transaction_aggregates_period",
                table: "transaction_aggregates",
                sql: "period IN ('Day', 'Month')");
        }
    }
}
