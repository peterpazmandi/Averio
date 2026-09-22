using Averio.Domain.Categories;
using Averio.Domain.Transactions;

namespace Averio.Application.Common.Abstractions;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAvailableAsync(
        Guid householdId,
        TransactionKind? kind,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Category>> GetAvailableByIdsAsync(
        Guid householdId,
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken);
}
