using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

public partial class AddTransactionDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "merchant",
            table: "transactions",
            type: "character varying(160)",
            maxLength: 160,
            nullable: true);

        migrationBuilder.AddUniqueConstraint(
            name: "ak_transactions_household_id_id",
            table: "transactions",
            columns: new[] { "household_id", "id" });

        migrationBuilder.CreateTable(
            name: "transaction_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: false),
                transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                position = table.Column<int>(type: "integer", nullable: false),
                description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                category_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                amount = table.Column<decimal>(type: "numeric(19,4)", precision: 19, scale: 4, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transaction_items", x => x.id);
                table.CheckConstraint("ck_transaction_items_amount", "amount > 0");
                table.CheckConstraint("ck_transaction_items_position", "position >= 0");
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
                id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: false),
                transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: true),
                scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_transaction_labels", x => x.id);
                table.CheckConstraint("ck_transaction_labels_scope", "scope IN ('Household', 'Account')");
                table.CheckConstraint(
                    "ck_transaction_labels_scope_account",
                    "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
                table.ForeignKey(
                    name: "fk_transaction_labels_accounts",
                    columns: x => new { x.household_id, x.account_id },
                    principalTable: "accounts",
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
            name: "ux_transaction_items_transaction_position",
            table: "transaction_items",
            columns: new[] { "household_id", "transaction_id", "position" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_transaction_labels_account",
            table: "transaction_labels",
            columns: new[] { "household_id", "account_id" });

        migrationBuilder.CreateIndex(
            name: "ix_transaction_labels_transaction",
            table: "transaction_labels",
            columns: new[] { "household_id", "transaction_id" });

        // Existing development transactions become one-item transactions so
        // the total remains exactly the same after the model change.
        migrationBuilder.Sql(
            """
            INSERT INTO transaction_items
                (id, household_id, transaction_id, position, description, category_name, amount)
            SELECT
                id, household_id, id, 0, description, NULL, amount
            FROM transactions;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "transaction_items");
        migrationBuilder.DropTable(name: "transaction_labels");

        migrationBuilder.DropUniqueConstraint(
            name: "ak_transactions_household_id_id",
            table: "transactions");

        migrationBuilder.DropColumn(
            name: "merchant",
            table: "transactions");
    }
}
