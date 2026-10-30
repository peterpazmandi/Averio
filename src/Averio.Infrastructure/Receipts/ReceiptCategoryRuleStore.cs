using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Receipts;

public sealed class ReceiptCategoryRuleStore(AverioDbContext db) : IReceiptCategoryRuleStore
{
    public async Task<IReadOnlyDictionary<string, Guid>> GetAsync(Guid householdId, CancellationToken ct)
        => await db.Set<ReceiptCategoryRule>().AsNoTracking().Where(r => r.HouseholdId == householdId)
            .ToDictionaryAsync(r => r.MatchKey, r => r.CategoryId, ct);

    // Posting owns the household lock and transaction. Last explicitly confirmed choice wins.
    public async Task UpsertAsync(Guid householdId, Guid memberId, string matchKey, Guid categoryId, DateTimeOffset now, CancellationToken ct)
        => await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO receipt_category_rules (household_id, match_key, category_id, updated_by_member_id, created_at, updated_at)
            VALUES ({householdId}, {matchKey}, {categoryId}, {memberId}, {now}, {now})
            ON CONFLICT (household_id, match_key) DO UPDATE SET category_id = EXCLUDED.category_id,
                updated_by_member_id = EXCLUDED.updated_by_member_id, updated_at = EXCLUDED.updated_at
            """, ct);
}
