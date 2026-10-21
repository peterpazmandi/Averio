using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations;

/// <summary>
/// Repairs databases that already applied an earlier version of AddRecurringPersonalMoney
/// before the immutable create-request fields were added to that migration.
/// The original migration now includes these fields for fresh installations.
/// </summary>
public partial class RepairRecurringAllowanceCreationColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The table exists at this point for both fresh and previously upgraded databases.
        // Only backfill if a column is newly added: original create-request values in
        // properly migrated databases must never be overwritten by later schedule edits.
        migrationBuilder.Sql("""
            DO $repair$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'recurring_allowance_rules'
                      AND column_name = 'creation_amount'
                ) THEN
                    ALTER TABLE recurring_allowance_rules
                        ADD COLUMN creation_amount numeric(19,4);
                    UPDATE recurring_allowance_rules
                        SET creation_amount = amount;
                END IF;

                IF NOT EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = current_schema()
                      AND table_name = 'recurring_allowance_rules'
                      AND column_name = 'creation_ends_on'
                ) THEN
                    ALTER TABLE recurring_allowance_rules
                        ADD COLUMN creation_ends_on date;
                    UPDATE recurring_allowance_rules
                        SET creation_ends_on = ends_on;
                END IF;

                ALTER TABLE recurring_allowance_rules
                    ALTER COLUMN creation_amount SET NOT NULL;
            END
            $repair$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately non-destructive: these columns belong to the previous migration
        // on new databases, and may contain historical idempotency metadata.
    }
}
