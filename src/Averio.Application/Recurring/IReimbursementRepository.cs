using Averio.Domain.Recurring;
namespace Averio.Application.Recurring;
public interface IReimbursementRepository
{
    void Add(ReimbursementClaim claim);
    Task<ReimbursementClaim?> FindForTransactionAsync(Guid householdId, Guid transactionId, CancellationToken ct);
    Task<IReadOnlyList<ReimbursementClaim>> ListAsync(Guid householdId, Guid memberId, Guid? transactionId, CancellationToken ct);
    Task<ReimbursementClaim?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
}
