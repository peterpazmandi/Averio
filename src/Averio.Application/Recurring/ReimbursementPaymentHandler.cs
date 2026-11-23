using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.Refunds;
using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
namespace Averio.Application.Recurring;

public sealed record LinkReimbursementPayment(Guid CreditTransactionId, Guid OperationId, Guid ExpectedVersion,
    DateTimeOffset ExpectedCreditUpdatedAt, bool MoneyReceived);
public sealed class ReimbursementPaymentHandler(ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ITransactionRepository transactions, IReimbursementRepository claims, IItemRefundRepository dependencies, IUnitOfWork uow, TimeProvider clock)
{
    private async Task<(HouseholdMember Member, ReimbursementClaim Claim, Transaction Source)> Context(Guid h, Guid id, bool write, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(h, uid, ct) ?? throw new NotFoundException("household", h);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        var member = household.Members.Single(m => m.UserId == uid && m.LeftAtUtc == null);
        var claim = await claims.GetAsync(h, member.Id, id, ct) ?? throw new NotFoundException("reimbursement", id);
        var source = await transactions.GetVisibleByIdAsync(h, member.Id, claim.TransactionId, ct) ?? throw new NotFoundException("reimbursement", id);
        var account = await accounts.GetVisibleByIdAsync(h, member.Id, source.AccountId, ct) ?? throw new NotFoundException("reimbursement", id);
        if (write && !TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        return (member, claim, source);
    }
    public async Task<IReadOnlyList<Transaction>> CandidatesAsync(Guid h, Guid id, string? search, int offset, CancellationToken ct)
    {
        var (member, claim, source) = await Context(h, id, true, ct);
        if (offset < 0 || offset > 100000 || search?.Length > 160) throw Invalid();
        await Destination(h, member, claim, ct);
        if (claim.Status is not (ReimbursementStatus.Submitted or ReimbursementStatus.PartiallyReceived)
            || source.Status != TransactionStatus.Posted || source.Kind != TransactionKind.Expense || source.IsArchived) return [];
        return await claims.PaymentCandidatesAsync(h, member.Id, claim, Math.Min(claim.OutstandingAmount, source.Amount - claim.ReceivedAmount), search?.Trim(), offset, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct);
    }
    private async Task Destination(Guid h, HouseholdMember member, ReimbursementClaim claim, CancellationToken ct)
    {
        var destination = await accounts.GetVisibleByIdAsync(h, member.Id, claim.DestinationAccountId, ct) ?? throw new NotFoundException("reimbursement", claim.Id);
        if (!TransactionAccess.CanModify(destination, member)) throw new ForbiddenException();
        if (destination.Status != AccountStatus.Active || destination.CutoverAtUtc is not null) throw Invalid();
    }
    public async Task<Guid> LinkAsync(Guid h, Guid id, LinkReimbursementPayment request, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var (member, claim, source) = await Context(h, id, true, ct);
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(new { id, request }))));
        var previous = await claims.PaymentByOperationAsync(h, request.OperationId, ct);
        if (previous is not null)
        {
            if (previous.ClaimId != id || previous.LinkedByMemberId != member.Id || previous.RequestFingerprint != fingerprint) throw Changed();
            return previous.Id; // Includes historical/unlinked attempts; never relink on retry.
        }
        if (claim.Version != request.ExpectedVersion) throw Changed();
        await Destination(h, member, claim, ct);
        var credit = await transactions.GetVisibleByIdAsync(h, member.Id, request.CreditTransactionId, ct) ?? throw new NotFoundException("transaction", request.CreditTransactionId);
        if (credit.UpdatedAt != request.ExpectedCreditUpdatedAt) throw Changed();
        if (!request.MoneyReceived || source.Status != TransactionStatus.Posted || source.Kind != TransactionKind.Expense || source.IsArchived
            || claim.ReceivedAmount + credit.Amount > source.Amount || await dependencies.CreditHasDependenciesAsync(h, credit.Id, ct)) throw Invalid();
        // The household write lock is held: read current allocations before consuming this credit.
        var allocations = await claims.ActiveCreditAllocationsAsync(h, credit.Id, ct);
        try { ReceivedCreditAllocationRule.ValidateNew(credit.Amount, allocations, credit.Amount,
            Math.Min(claim.OutstandingAmount, source.Amount - claim.ReceivedAmount)); }
        catch (ArgumentException) { throw Invalid(); }
        ReimbursementPayment payment;
        try { payment = claim.LinkPayment(credit, request.OperationId, fingerprint, member.Id, clock.GetUtcNow()); }
        catch (ArgumentException) { throw Invalid(); }
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return payment.Id;
    }
    public async Task UnlinkAsync(Guid h, Guid id, Guid paymentId, Guid expectedVersion, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var (member, claim, _) = await Context(h, id, true, ct);
        var payment = claim.Payments.SingleOrDefault(p => p.Id == paymentId) ?? throw new NotFoundException("payment", paymentId);
        if (payment.UnlinkedAt is not null) return; // An old unlink cannot affect a new allocation.
        if (claim.Version != expectedVersion) throw Changed();
        claim.UnlinkPayment(paymentId, member.Id, clock.GetUtcNow());
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<HouseholdMember> RecoveryMember(Guid householdId, CancellationToken ct)
    {
        var uid = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, uid, ct) ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(m => m.UserId == uid && m.LeftAtUtc == null);
    }
    public async Task<IReadOnlyList<ReimbursementRecoveryLink>> RecoveryLinksAsync(Guid householdId, CancellationToken ct)
    {
        var member = await RecoveryMember(householdId, ct);
        return await claims.RecoveryLinksAsync(householdId, member.Id, ct);
    }
    public async Task RecoverAsync(Guid householdId, IReadOnlyList<ReimbursementRecoveryLink> links, CancellationToken ct)
    {
        if (links.Count is < 1 or > 100 || links.Distinct().Count() != links.Count) throw Invalid();
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await RecoveryMember(householdId, ct);
        foreach (var group in links.GroupBy(p => p.ClaimId))
        {
            // Removing the creator's own attribution is allowed without access to bank details.
            // Exact, never-reused payment IDs make retries safe even after a later relink.
            var claim = await claims.GetOwnedAsync(householdId, member.Id, group.Key, ct)
                ?? throw new NotFoundException("reimbursement", group.Key);
            foreach (var link in group)
            {
                if (!claim.Payments.Any(p => p.Id == link.PaymentId)) throw new NotFoundException("payment", link.PaymentId);
                claim.UnlinkPayment(link.PaymentId, member.Id, clock.GetUtcNow());
            }
        }
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private static BusinessRuleException Invalid() => new("reimbursement_payment_invalid", "Validation_ReimbursementPayment");
    private static ConflictException Changed() => new("reimbursement_changed", "Error_ReimbursementChanged");
}

