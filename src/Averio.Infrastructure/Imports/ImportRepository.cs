using Averio.Application.Imports;
using Averio.Domain.Imports;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Averio.Infrastructure.Imports;

internal sealed partial class ImportRepository(AverioDbContext db) : IImportRepository
{
    public void Add(ImportBatch batch) => db.Set<ImportBatch>().Add(batch);
    public Task<ImportBatch?> FindAsync(Guid householdId, Guid memberId, string fingerprint, CancellationToken ct)
        => db.Set<ImportBatch>().SingleOrDefaultAsync(b => b.HouseholdId == householdId && b.MemberId == memberId && b.Fingerprint == fingerprint, ct);
    public Task<ImportBatch?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct)
        => db.Set<ImportBatch>().SingleOrDefaultAsync(b => b.HouseholdId == householdId && b.MemberId == memberId && b.Id == id, ct);
    public async Task ApplyAggregatesAsync(Guid[] transactionIds, CancellationToken ct, int direction = 1)
    {
        // One set-based upsert avoids tens of thousands of database round trips.
        if (direction is not (1 or -1)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (transactionIds.Length == 0) return;
        if (direction == -1)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                WITH delta (household_id, account_id, dimension_type, dimension_id, currency_code, period, period_start, income_amount, expense_amount, entry_count, updated_at) AS (
            SELECT t.household_id, t.account_id, d.dimension_type, d.dimension_id, t.currency_code,
                   p.period, p.period_start,
                   SUM(CASE WHEN t.kind = 'Income' THEN d.amount ELSE 0 END),
                   SUM(CASE WHEN t.kind = 'Expense' THEN d.amount ELSE 0 END), COUNT(*)::int, now()
            FROM transactions t
            CROSS JOIN LATERAL (
                SELECT 'Account'::text AS dimension_type, t.account_id AS dimension_id, t.amount
                UNION ALL SELECT 'Category', i.category_id, i.amount FROM transaction_items i WHERE i.transaction_id = t.id
            ) d
            CROSS JOIN LATERAL (VALUES
                ('Day', t.occurred_on), ('Month', date_trunc('month', t.occurred_on)::date),
                ('Year', date_trunc('year', t.occurred_on)::date)
            ) p(period, period_start)
            WHERE t.id = ANY({transactionIds}) AND t.visibility = 'Household' AND t.status = 'Posted'
            GROUP BY t.household_id, t.account_id, d.dimension_type, d.dimension_id, t.currency_code, p.period, p.period_start
            ORDER BY t.household_id, t.account_id, d.dimension_type, d.dimension_id, t.currency_code, p.period, p.period_start

                )
                UPDATE transaction_aggregates a
                SET income_amount = a.income_amount - d.income_amount,
                    expense_amount = a.expense_amount - d.expense_amount,
                    entry_count = a.entry_count - d.entry_count, updated_at = now()
                FROM delta d
                WHERE (a.household_id, a.account_id, a.dimension_type, a.dimension_id, a.currency_code, a.period, a.period_start)
                    = (d.household_id, d.account_id, d.dimension_type, d.dimension_id, d.currency_code, d.period, d.period_start);
            """, ct);
            return;
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO transaction_aggregates
                (household_id, account_id, dimension_type, dimension_id, currency_code, period, period_start,
                 income_amount, expense_amount, entry_count, updated_at)
            SELECT t.household_id, t.account_id, d.dimension_type, d.dimension_id, t.currency_code,
                   p.period, p.period_start,
                   {direction} * SUM(CASE WHEN t.kind = 'Income' THEN d.amount ELSE 0 END),
                   {direction} * SUM(CASE WHEN t.kind = 'Expense' THEN d.amount ELSE 0 END), {direction} * COUNT(*)::int, now()
            FROM transactions t
            CROSS JOIN LATERAL (
                SELECT 'Account'::text AS dimension_type, t.account_id AS dimension_id, t.amount
                UNION ALL SELECT 'Category', i.category_id, i.amount FROM transaction_items i WHERE i.transaction_id = t.id
            ) d
            CROSS JOIN LATERAL (VALUES
                ('Day', t.occurred_on), ('Month', date_trunc('month', t.occurred_on)::date),
                ('Year', date_trunc('year', t.occurred_on)::date)
            ) p(period, period_start)
            WHERE t.id = ANY({transactionIds}) AND t.visibility = 'Household' AND t.status = 'Posted'
            GROUP BY t.household_id, t.account_id, d.dimension_type, d.dimension_id, t.currency_code, p.period, p.period_start
            ORDER BY t.household_id, t.account_id, d.dimension_type, d.dimension_id, t.currency_code, p.period, p.period_start
            ON CONFLICT (household_id, account_id, dimension_type, dimension_id, currency_code, period, period_start)
            DO UPDATE SET income_amount = transaction_aggregates.income_amount + EXCLUDED.income_amount,
                expense_amount = transaction_aggregates.expense_amount + EXCLUDED.expense_amount,
                entry_count = transaction_aggregates.entry_count + EXCLUDED.entry_count, updated_at = EXCLUDED.updated_at;
            """, ct);
    }
}
