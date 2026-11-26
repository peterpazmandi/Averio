using Averio.Domain.Transactions;
namespace Averio.Application.Transactions.Refunds;
public interface IItemRefundRepository
{
    void Add(ItemRefund refund);
    Task<ItemRefund?> ByOperationAsync(Guid householdId, Guid operationId, CancellationToken ct);
    Task<ItemRefund?> GetAsync(Guid householdId, Guid id, CancellationToken ct);
    Task<ItemRefund?> ByCreditAsync(Guid householdId, Guid creditId, CancellationToken ct);
    Task<IReadOnlyList<ItemRefund>> ListAsync(Guid householdId, Guid memberId, Guid transactionId, CancellationToken ct);
    Task<bool> CreditHasDependenciesAsync(Guid householdId, Guid transactionId, CancellationToken ct);
}
