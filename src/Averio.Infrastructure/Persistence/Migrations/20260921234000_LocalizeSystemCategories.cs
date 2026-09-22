using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

public partial class LocalizeSystemCategories : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_categories_base_code",
            table: "categories");

        migrationBuilder.AddColumn<string>(
            name: "system_key",
            table: "categories",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE categories
            SET system_key = code
            WHERE is_base;
            """);

        migrationBuilder.Sql(
            """
            UPDATE transaction_items AS item
            SET category_name = category.system_key
            FROM categories AS category
            WHERE category.id = item.category_id
              AND category.is_base;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "categories",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100);

        migrationBuilder.Sql(
            """
            UPDATE categories
            SET name = NULL
            WHERE is_base;
            """);

        migrationBuilder.DropColumn(
            name: "code",
            table: "categories");

        migrationBuilder.AddCheckConstraint(
            name: "ck_categories_name_source",
            table: "categories",
            sql: "(is_base AND system_key IS NOT NULL AND name IS NULL) OR "
                + "(NOT is_base AND system_key IS NULL AND name IS NOT NULL)");

        migrationBuilder.CreateIndex(
            name: "ux_categories_system_key",
            table: "categories",
            column: "system_key",
            unique: true,
            filter: "is_base");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_categories_system_key",
            table: "categories");

        migrationBuilder.DropCheckConstraint(
            name: "ck_categories_name_source",
            table: "categories");

        migrationBuilder.AddColumn<string>(
            name: "code",
            table: "categories",
            type: "character varying(80)",
            maxLength: 80,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE categories
            SET
                code = COALESCE(system_key, lower(regexp_replace(name, '[^a-zA-Z0-9]+', '-', 'g'))),
                name = COALESCE(name, system_key);
            """);

        migrationBuilder.AlterColumn<string>(
            name: "name",
            table: "categories",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "code",
            table: "categories",
            type: "character varying(80)",
            maxLength: 80,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(80)",
            oldMaxLength: 80,
            oldNullable: true);

        migrationBuilder.DropColumn(
            name: "system_key",
            table: "categories");

        migrationBuilder.CreateIndex(
            name: "ux_categories_base_code",
            table: "categories",
            column: "code",
            unique: true,
            filter: "is_base");
    }
}
