using Averio.Domain.Transactions;
namespace Averio.Application.Transactions.SavedViews;

public sealed record SavedViewFilters(Guid? Account, string? Kind, string Period, DateOnly? From, DateOnly? To, Guid? Activity, Guid[] Labels);
public sealed record SavedViewInput(string Name, SavedViewFilters Filters);
public sealed record SavedViewResult(Guid Id, string Name, SavedViewFilters Filters, Guid Version, bool Available);
public interface ISavedViewRepository
{
    Task LockMemberAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<IReadOnlyList<SavedTransactionView>> ListAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<SavedTransactionView?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    void Add(SavedTransactionView view);
    void Remove(SavedTransactionView view);
}
