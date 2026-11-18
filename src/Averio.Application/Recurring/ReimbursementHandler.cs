using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
namespace Averio.Application.Recurring;

public sealed record ReimbursementResult(ReimbursementClaim Claim, string Description, bool CanModify, bool PaymentUnavailable, bool PaymentChanged);
public sealed class ReimbursementHandler(ICurrentUser currentUser, IHouseholdRepository households,
    IAccountRepository accounts, ITransactionRepository transactions, IReimbursementRepository claims, IUnitOfWork uow, TimeProvider clock)
{
    private async Task<Averio.Domain.Households.HouseholdMember> Member(Guid householdId, CancellationToken ct)
    {
        var user = currentUser.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, user, ct) ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(m => m.UserId == user && m.LeftAtUtc is null);
    }
    public async Task<IReadOnlyList<ReimbursementResult>> ListAsync(Guid householdId, Guid? transactionId, CancellationToken ct)
    {
        var member = await Member(householdId, ct);
        var results = new List<ReimbursementResult>();
        foreach (var claim in await claims.ListAsync(householdId, member.Id, transactionId, ct))
        {
            var payment = await transactions.GetVisibleByIdAsync(householdId, member.Id, claim.TransactionId, ct);
            if (payment is null) continue;
            var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, payment.AccountId, ct);
            results.Add(new(claim, payment.Description, account is not null && TransactionAccess.CanModify(account, member),
                (payment.Status != TransactionStatus.Posted || payment.Kind != TransactionKind.Expense), payment.Amount != claim.OriginalPaymentAmount || payment.OccurredOn != claim.PaymentOn));
        }
        return results;
    }
    public async Task ChangeAsync(Guid householdId, Guid id, Guid expectedVersion, bool confirmed,
        DateOnly? submittedOn, string? reference, ReimbursementStatus status, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, ct);
        var claim = await claims.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("reimbursement", id);
        var payment = await transactions.GetVisibleByIdAsync(householdId, member.Id, claim.TransactionId, ct)
            ?? throw new NotFoundException("reimbursement", id);
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, payment.AccountId, ct)
            ?? throw new NotFoundException("reimbursement", id);
        if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        if (claim.Version != expectedVersion) throw new ConflictException("reimbursement_changed", "Error_ReimbursementChanged");
        try
        {
            if (status == ReimbursementStatus.Submitted)
            {
                if ((payment.Status != TransactionStatus.Posted || payment.Kind != TransactionKind.Expense) || submittedOn is null) throw new ArgumentException();
                claim.Submit(confirmed, submittedOn.Value, reference, member.Id, clock.GetUtcNow());
            }
            else claim.Close(status, member.Id, clock.GetUtcNow());
        }
        catch (ArgumentException) { throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement"); }
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
