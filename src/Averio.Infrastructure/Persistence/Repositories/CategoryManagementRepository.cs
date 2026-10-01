using Averio.Application.Categories.ManageCategories;
using Averio.Domain.Categories;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class CategoryManagementRepository(AverioDbContext db) : ICategoryManagementRepository
{
    public async Task LockHouseholdAsync(Guid householdId, CancellationToken ct)
    {
        await db.Households.FromSqlInterpolated(
            $"SELECT * FROM households WHERE id = {householdId} FOR UPDATE").AsNoTracking().ToListAsync(ct);
    }

    public Task<Category?> GetAsync(Guid householdId, Guid categoryId, CancellationToken ct)
        => db.Categories.SingleOrDefaultAsync(
            c => c.Id == categoryId && (c.IsBase || c.HouseholdId == householdId), ct);

    public Task<bool> HasChildrenAsync(Guid categoryId, bool activeOnly, CancellationToken ct)
        => db.Categories.AnyAsync(c => c.ParentId == categoryId && (!activeOnly || !c.IsArchived), ct);

    public void Add(Category category) => db.Categories.Add(category);
}
