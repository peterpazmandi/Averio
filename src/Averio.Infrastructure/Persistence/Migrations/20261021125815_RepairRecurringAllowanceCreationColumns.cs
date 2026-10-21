
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Averio.Infrastructure.Persistence.Migrations
{
    public partial class RepairRecurringAllowanceCreationColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Add the immutable creation amount if missing.
                ALTER TABLE recurring_allowance_rules
                    ADD COLUMN IF NOT EXISTS creation_amount numeric(19,4);

                -- Preserve existing values. Recover missing values
                -- from the current amount where necessary.
                UPDATE recurring_allowance_rules
                SET creation_amount = amount
                WHERE creation_amount IS NULL;

                ALTER TABLE recurring_allowance_rules
                    ALTER COLUMN creation_amount SET NOT NULL;

                -- Only backfill creation_ends_on if the column
                -- is newly added. Existing NULL values may be
                -- legitimate original creation values.
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1
                        FROM pg_attribute
                        WHERE attrelid = 'recurring_allowance_rules'::regclass
                          AND attname = 'creation_ends_on'
                          AND NOT attisdropped
                    ) THEN
                        ALTER TABLE recurring_allowance_rules
                            ADD COLUMN creation_ends_on date;

                        UPDATE recurring_allowance_rules
                        SET creation_ends_on = ends_on;
                    END IF;
                END
                $$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty.
            //
            // The original AddRecurringPersonalMoney migration
            // already defines these columns in the current code.
            //
            // Removing them here would break fresh databases
            // where the original migration created them.
        }
    }
}
