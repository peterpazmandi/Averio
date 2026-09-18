using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

public partial class SeparateAccountScopeAndOwners : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Fail rather than silently repairing or dropping inconsistent legacy data.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM accounts a
                    LEFT JOIN households h ON h.id = a.household_id
                    WHERE h.id IS NULL
                ) THEN
                    RAISE EXCEPTION 'Accounts reference a missing household. Resolve before migrating.';
                END IF;
                IF EXISTS (
                    SELECT 1 FROM accounts
                    WHERE ownership NOT IN ('Household', 'Personal')
                       OR visibility NOT IN ('Household', 'Private')
                       OR type NOT IN ('Checking', 'Savings', 'Cash', 'CreditCard')
                       OR status NOT IN ('Active', 'Archived')
                       OR (ownership = 'Personal' AND owner_household_member_id IS NULL)
                ) THEN
                    RAISE EXCEPTION 'Invalid legacy account values. Resolve before migrating.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropCheckConstraint(
            name: "ck_accounts_ownership_visibility", table: "accounts");

        // A rename preserves existing Household/Personal values.
        migrationBuilder.RenameColumn(
            name: "ownership", table: "accounts", newName: "scope");

        migrationBuilder.AddUniqueConstraint(
            name: "ak_accounts_household_id_id",
            table: "accounts", columns: new[] { "household_id", "id" });

        migrationBuilder.AddForeignKey(
            name: "fk_accounts_households_household_id",
            table: "accounts", column: "household_id",
            principalTable: "households", principalColumn: "id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateTable(
            name: "account_owners",
            columns: table => new
            {
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                household_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                household_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_account_owners",
                    x => new { x.account_id, x.household_member_id });
                table.ForeignKey(
                    name: "fk_account_owners_accounts",
                    columns: x => new { x.household_id, x.account_id },
                    principalTable: "accounts",
                    principalColumns: new[] { "household_id", "id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_account_owners_members",
                    columns: x => new { x.household_id, x.household_member_id },
                    principalTable: "household_members",
                    principalColumns: new[] { "household_id", "id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ix_account_owners_household_account", table: "account_owners",
            columns: new[] { "household_id", "account_id" });
        migrationBuilder.CreateIndex(
            name: "ix_account_owners_household_member", table: "account_owners",
            columns: new[] { "household_id", "household_member_id" });

        // Copy first, remove the old column only after the copy succeeds.
        migrationBuilder.Sql("""
            INSERT INTO account_owners (household_id, account_id, household_member_id)
            SELECT household_id, id, owner_household_member_id
            FROM accounts
            WHERE owner_household_member_id IS NOT NULL;
            """);

        migrationBuilder.DropForeignKey(
            name: "FK_accounts_household_members_household_id_owner_household_mem~",
            table: "accounts");
        migrationBuilder.DropIndex(
            name: "IX_accounts_household_id_owner_household_member_id", table: "accounts");
        migrationBuilder.DropIndex(
            name: "IX_accounts_owner_household_member_id", table: "accounts");
        migrationBuilder.DropColumn(name: "owner_household_member_id", table: "accounts");

        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_scope", table: "accounts",
            sql: "scope IN ('Household', 'Personal')");
        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_visibility", table: "accounts",
            sql: "visibility IN ('Household', 'Private')");
        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_type", table: "accounts",
            sql: "type IN ('Checking', 'Savings', 'Cash', 'CreditCard')");
        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_status", table: "accounts",
            sql: "status IN ('Active', 'Archived')");
        migrationBuilder.AddCheckConstraint(
            name: "ck_accounts_scope_visibility", table: "accounts",
            sql: """
                (scope = 'Household' AND visibility = 'Household' AND include_in_household_reports)
                OR
                (scope = 'Personal' AND (visibility <> 'Private' OR NOT include_in_household_reports))
                """);

        // A normal CHECK cannot enforce the existence of a child row.
        // Defer until commit so EF can insert the account followed by its holders.
        migrationBuilder.Sql("""
            CREATE FUNCTION enforce_personal_account_holders()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            DECLARE
                old_account uuid;
                new_account uuid;
                checked_account uuid;
                checked_scope text;
            BEGIN
                IF TG_TABLE_NAME = 'accounts' THEN
                    IF TG_OP <> 'INSERT' THEN old_account := OLD.id; END IF;
                    IF TG_OP <> 'DELETE' THEN new_account := NEW.id; END IF;
                ELSE
                    IF TG_OP <> 'INSERT' THEN old_account := OLD.account_id; END IF;
                    IF TG_OP <> 'DELETE' THEN new_account := NEW.account_id; END IF;
                END IF;

                FOR checked_account IN
                    SELECT DISTINCT value
                    FROM unnest(ARRAY[old_account, new_account]) AS ids(value)
                    WHERE value IS NOT NULL
                    ORDER BY value
                LOOP
                    -- Serialize checks against changes to the same parent.
                    SELECT scope INTO checked_scope
                    FROM accounts WHERE id = checked_account FOR UPDATE;

                    -- A deleted account needs no holders.
                    IF FOUND AND checked_scope = 'Personal' THEN
                        IF NOT EXISTS (
                            SELECT 1 FROM account_owners
                            WHERE account_id = checked_account
                        ) THEN
                            RAISE EXCEPTION 'Personal accounts require at least one holder.'
                                USING ERRCODE = '23514',
                                      CONSTRAINT = 'ck_personal_account_has_holder';
                        END IF;
                    END IF;
                END LOOP;
                RETURN NULL;
            END;
            $$;

            CREATE CONSTRAINT TRIGGER ck_personal_account_holders_parent
            AFTER INSERT OR UPDATE ON accounts
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION enforce_personal_account_holders();

            CREATE CONSTRAINT TRIGGER ck_personal_account_holders_children
            AFTER INSERT OR UPDATE OR DELETE ON account_owners
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION enforce_personal_account_holders();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The old schema cannot represent joint holders or household-scope
        // accounts with named holders. Never silently discard that information.
        throw new NotSupportedException(
            "This migration has no lossless automatic downgrade. "
            + "Restore the pre-migration backup or write an explicit reviewed reverse migration.");
    }
}
