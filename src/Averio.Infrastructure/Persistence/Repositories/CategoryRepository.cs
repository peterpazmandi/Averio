using Averio.Application.Common.Abstractions;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class CategoryRepository(AverioDbContext dbContext)
    : ICategoryRepository
{
    public async Task<IReadOnlyList<Category>> GetAvailableAsync(
        Guid householdId,
        TransactionKind? kind,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                !category.IsArchived
                && (category.IsBase || category.HouseholdId == householdId));

        if (kind is not null)
        {
            var requested = kind.Value == TransactionKind.Income
                ? CategoryKind.Income
                : CategoryKind.Expense;

            query = query.Where(category =>
                category.Kind == requested
                || category.Kind == CategoryKind.Both);
        }

        return await query
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Category>> GetAvailableByIdsAsync(
        Guid householdId,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        return dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                categoryIds.Contains(category.Id)
                && !category.IsArchived
                && (category.IsBase || category.HouseholdId == householdId))
            .ToListAsync(cancellationToken)
            .ContinueWith(
                task => (IReadOnlyList<Category>)task.Result,
                cancellationToken,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}
