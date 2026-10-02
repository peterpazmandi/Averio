using Averio.Application.Categories.ManageCategories;
using Averio.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class CategoryPinRepository(AverioDbContext db) : ICategoryPinRepository
{
    public async Task<IReadOnlyDictionary<Guid, int>> GetPinsAsync(Guid memberId, CancellationToken ct)
        => await db.Set<CategoryPin>().AsNoTracking().Where(p => p.HouseholdMemberId == memberId)
            .ToDictionaryAsync(p => p.CategoryId, p => p.SortOrder, ct);

    public async Task ReorderAsync(Guid memberId, Guid[] ids, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("A transaction is required.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE category_pins SET sort_order = array_position({ids}, category_id) - 1
            WHERE household_member_id = {memberId}
            """, ct);
    }

    public async Task SetAsync(Guid memberId, Guid categoryId, bool pinned, DateTimeOffset now, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Pin changes require an authorized household transaction.");
        if (pinned)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO category_pins (household_member_id, category_id, created_at, sort_order)
                SELECT {memberId}, {categoryId}, {now}, COALESCE(MAX(sort_order), -1) + 1
                FROM category_pins WHERE household_member_id = {memberId}
                ON CONFLICT (household_member_id, category_id) DO NOTHING
                """, ct);
        else
            await db.Set<CategoryPin>().Where(p => p.HouseholdMemberId == memberId && p.CategoryId == categoryId)
                .ExecuteDeleteAsync(ct);
    }
}
