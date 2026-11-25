using Averio.Domain.Recurring;
namespace Averio.Application.Recurring;
public interface IReimbursementRepository
{
    Task<IReadOnlyList<decimal>> ActiveCreditAllocationsAsync(Guid householdId, Guid creditId, CancellationToken ct);
    Task<ReimbursementPayment?> PaymentByOperationAsync(Guid householdId, Guid operationId, CancellationToken ct);
    Task<IReadOnlySet<Guid>> PaymentProtectedIdsAsync(Guid householdId, IReadOnlyList<Guid> transactionIds, CancellationToken ct);
    Task<bool> HasActivePaymentAsync(Guid householdId, Guid transactionId, CancellationToken ct);
    Task<IReadOnlyList<ReimbursementCreditCandidate>> PaymentCandidatesAsync(Guid householdId, Guid memberId, ReimbursementClaim claim, decimal available, string? search, int offset, DateOnly today, CancellationToken ct);
    Task<IReadOnlyList<ReimbursementRecoveryLink>> RecoveryLinksAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<IReadOnlyList<ReimbursementDestinationRecovery>> DestinationRecoveryAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<ReimbursementClaim?> GetOwnedAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    void Add(ReimbursementClaim claim);
    Task<IReadOnlyDictionary<Guid, string>> StatusesAsync(Guid householdId, Guid memberId, IReadOnlyList<Guid> transactionIds, CancellationToken ct);
    Task<ReimbursementClaim?> FindForTransactionAsync(Guid householdId, Guid transactionId, CancellationToken ct);
    Task<IReadOnlyList<ReimbursementClaim>> ListAsync(Guid householdId, Guid memberId, Guid? transactionId, CancellationToken ct, bool includeCancelled = false);
    Task<ReimbursementClaim?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
}

public sealed record ReimbursementRecoveryLink(Guid ClaimId, Guid PaymentId);
public sealed record ReimbursementDestinationRecovery(Guid ClaimId, Guid Version);
public sealed record ReimbursementCreditCandidate(Averio.Domain.Transactions.Transaction Credit, decimal AvailableAmount, decimal MaximumAllocation);

