using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
namespace Averio.Application.Recurring;

public sealed record ReimbursementResult(ReimbursementClaim Claim, string Description, bool CanModify, bool PaymentUnavailable, bool PaymentChanged,
    IReadOnlySet<Guid> VisibleAccountIds);
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
        var visibleAccountIds = (await accounts.GetVisibleAsync(householdId, member.Id, true, ct)).Select(a => a.Id).ToHashSet();
        var results = new List<ReimbursementResult>();
        foreach (var claim in await claims.ListAsync(householdId, member.Id, transactionId, ct))
        {
            var payment = await transactions.GetVisibleByIdAsync(householdId, member.Id, claim.TransactionId, ct);
            if (payment is null) continue;
            var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, payment.AccountId, ct);
            results.Add(new(claim, payment.Description, account is not null && TransactionAccess.CanModify(account, member),
                (payment.Status != TransactionStatus.Posted || payment.Kind != TransactionKind.Expense), payment.Amount != claim.OriginalPaymentAmount || payment.OccurredOn != claim.PaymentOn,
                visibleAccountIds));
        }
        return results;
    }
    public async Task<Guid> CreateAsync(Guid householdId, Guid transactionId, DateTimeOffset expectedUpdatedAt,
        RecurringReimbursementRule configuration, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, ct);
        var payment = await transactions.GetVisibleByIdAsync(householdId, member.Id, transactionId, ct)
            ?? throw new NotFoundException("transaction", transactionId);
        var id = await CreateWithinTransactionAsync(payment, member, expectedUpdatedAt, configuration, ct);
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct); return id;
    }
    internal async Task<Guid> CreateWithinTransactionAsync(Transaction payment, Averio.Domain.Households.HouseholdMember member,
        DateTimeOffset expectedUpdatedAt, RecurringReimbursementRule configuration, CancellationToken ct)
    {
        var account = await accounts.GetVisibleByIdAsync(payment.HouseholdId, member.Id, payment.AccountId, ct)
            ?? throw new NotFoundException("transaction", payment.Id);
        if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        var identity = System.Text.Json.JsonSerializer.Serialize(new { UpdatedAt = expectedUpdatedAt.ToUniversalTime(),
            configuration.Provider, configuration.Calculation, configuration.Value, configuration.MaximumAmount,
            configuration.DestinationAccountId, configuration.PaymentDelayDays, configuration.SubmissionReminderDays });
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
        var previous = await claims.FindForTransactionAsync(payment.HouseholdId, payment.Id, ct);
        if (previous is not null && previous.HouseholdMemberId == member.Id && previous.CreationFingerprint == fingerprint)
            return previous.Id;
        var destination = await accounts.GetVisibleByIdAsync(payment.HouseholdId, member.Id, configuration.DestinationAccountId, ct)
            ?? throw new NotFoundException("account", configuration.DestinationAccountId);
        if (payment.Status != TransactionStatus.Posted || payment.Kind != TransactionKind.Expense || payment.IsArchived
            || destination.Status != Averio.Domain.Accounts.AccountStatus.Active || !TransactionAccess.CanModify(destination, member)
            || destination.Currency != payment.Currency)
            throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement");
        if (previous is not null) throw new ConflictException("reimbursement_changed", "Error_ReimbursementChanged");
        if (payment.UpdatedAt != expectedUpdatedAt) throw new ConflictException("transaction_changed", "Error_TransactionChanged");
        ReimbursementClaim claim;
        try { claim = ReimbursementClaim.CreateManual(payment, member.Id, configuration, fingerprint, clock.GetUtcNow()); }
        catch (ArgumentException) { throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement"); }
        claims.Add(claim); return claim.Id;
    }
    public async Task UpdateAsync(Guid householdId, Guid id, Guid expectedVersion, RecurringReimbursementRule configuration, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, ct);
        var claim = await claims.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("reimbursement", id);
        var payment = await transactions.GetVisibleByIdAsync(householdId, member.Id, claim.TransactionId, ct) ?? throw new NotFoundException("reimbursement", id);
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, payment.AccountId, ct) ?? throw new NotFoundException("reimbursement", id);
        if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        if (claim.Version != expectedVersion) throw new ConflictException("reimbursement_changed", "Error_ReimbursementChanged");
        var destination = await accounts.GetVisibleByIdAsync(householdId, member.Id, configuration.DestinationAccountId, ct) ?? throw new NotFoundException("account", configuration.DestinationAccountId);
        if (payment.Status != TransactionStatus.Posted || payment.Kind != TransactionKind.Expense || payment.IsArchived
            || destination.Status != Averio.Domain.Accounts.AccountStatus.Active || !TransactionAccess.CanModify(destination, member) || destination.Currency.Value != claim.Currency)
            throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement");
        try { claim.Update(configuration, member.Id, clock.GetUtcNow()); }
        catch (ArgumentException) { throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement"); }
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<IReadOnlyList<ReimbursementDestinationRecovery>> DestinationRecoveryAsync(Guid householdId, CancellationToken ct)
    {
        var member = await Member(householdId, ct);
        return await claims.DestinationRecoveryAsync(householdId, member.Id, ct);
    }
    public async Task ReassignDestinationAsync(Guid householdId, Guid id, Guid expectedVersion, Guid destinationId, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, ct);
        var claim = await claims.GetOwnedAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("reimbursement", id);
        var source = await transactions.GetVisibleByIdAsync(householdId, member.Id, claim.TransactionId, ct)
            ?? throw new NotFoundException("reimbursement", id);
        var sourceAccount = await accounts.GetVisibleByIdAsync(householdId, member.Id, source.AccountId, ct)
            ?? throw new NotFoundException("reimbursement", id);
        if (!TransactionAccess.CanModify(sourceAccount, member)) throw new ForbiddenException();
        if (claim.Version != expectedVersion) throw new ConflictException("reimbursement_changed", "Error_ReimbursementChanged");
        if (await accounts.GetVisibleByIdAsync(householdId, member.Id, claim.DestinationAccountId, ct) is not null)
            throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement");
        var destination = await accounts.GetVisibleByIdAsync(householdId, member.Id, destinationId, ct)
            ?? throw new NotFoundException("account", destinationId);
        if (destination.Status != Averio.Domain.Accounts.AccountStatus.Active || !TransactionAccess.CanModify(destination, member)
            || destination.Currency.Value != claim.Currency)
            throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement");
        try { claim.ReassignDestination(destinationId, member.Id, clock.GetUtcNow()); }
        catch (ArgumentException) { throw new BusinessRuleException("reimbursement_invalid", "Validation_Reimbursement"); }
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
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
