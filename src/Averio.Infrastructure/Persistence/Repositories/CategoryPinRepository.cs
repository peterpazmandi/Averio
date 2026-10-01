using Averio.Application.Categories.ManageCategories;
using Averio.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class CategoryPinRepository(AverioDbContext db) : ICategoryPinRepository
{
    public async Task<IReadOnlySet<Guid>> GetPinnedIdsAsync(Guid memberId, CancellationToken ct)
        => (await db.Set<CategoryPin>().AsNoTracking().Where(p => p.HouseholdMemberId == memberId)
            .Select(p => p.CategoryId).ToListAsync(ct)).ToHashSet();

    public async Task SetAsync(Guid memberId, Guid categoryId, bool pinned, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Pin changes require an authorized household transaction.");
        if (pinned)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO category_pins (household_member_id, category_id, created_at)
                VALUES ({memberId}, {categoryId}, {now})
                ON CONFLICT (household_member_id, category_id) DO NOTHING
                """, ct);
        else
            await db.Set<CategoryPin>().Where(p => p.HouseholdMemberId == memberId && p.CategoryId == categoryId)
                .ExecuteDeleteAsync(ct);
    }
}
