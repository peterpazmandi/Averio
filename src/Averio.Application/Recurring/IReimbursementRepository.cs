using Averio.Domain.Recurring;
namespace Averio.Application.Recurring;
public interface IReimbursementRepository
{
    Task<ReimbursementPayment?> PaymentByOperationAsync(Guid householdId, Guid operationId, CancellationToken ct);
    Task<bool> HasActivePaymentAsync(Guid householdId, Guid transactionId, CancellationToken ct);
    Task<IReadOnlyList<Averio.Domain.Transactions.Transaction>> PaymentCandidatesAsync(Guid householdId, Guid memberId, ReimbursementClaim claim, decimal available, string? search, int offset, DateOnly today, CancellationToken ct);
    void Add(ReimbursementClaim claim);
    Task<IReadOnlyDictionary<Guid, string>> StatusesAsync(Guid householdId, Guid memberId, IReadOnlyList<Guid> transactionIds, CancellationToken ct);
    Task<ReimbursementClaim?> FindForTransactionAsync(Guid householdId, Guid transactionId, CancellationToken ct);
    Task<IReadOnlyList<ReimbursementClaim>> ListAsync(Guid householdId, Guid memberId, Guid? transactionId, CancellationToken ct);
    Task<ReimbursementClaim?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
}
