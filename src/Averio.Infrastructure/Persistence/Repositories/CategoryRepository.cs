using Averio.Application.Common.Abstractions;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(AverioDbContext dbContext) : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAvailableAsync(
        Guid householdId, TransactionKind? kind, CancellationToken cancellationToken, bool includeArchived = false)
    {
        var query = dbContext.Categories.AsNoTracking()
            .Where(c => (includeArchived || !c.IsArchived) && (c.IsBase || c.HouseholdId == householdId));
        if (kind is not null)
        {
            var requested = kind == TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;
            query = query.Where(c => c.Kind == requested || c.Kind == CategoryKind.Both);
        }
        return await query.OrderBy(c => c.SortOrder).ThenBy(c => c.SystemKey).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetAvailableByIdsAsync(
        Guid householdId, IReadOnlyCollection<Guid> categoryIds, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Category resolution requires a ledger transaction.");
        var ids = categoryIds.ToArray();
        return await dbContext.Categories.FromSqlInterpolated($"""
            SELECT * FROM categories
            WHERE id = ANY({ids}) AND (is_base OR household_id = {householdId})
            ORDER BY id FOR SHARE
            """).AsNoTracking().ToListAsync(cancellationToken);
    }
}
