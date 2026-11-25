using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.PersonalMoney;
using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
namespace Averio.Application.Transactions.Refunds;

public sealed record RecordItemRefund(Guid TransactionItemId, decimal Amount, DateOnly ReceivedOn, Guid OperationId,
    DateTimeOffset ExpectedUpdatedAt, bool MoneyReceived, Guid? CreditTransactionId = null, DateTimeOffset? ExpectedCreditUpdatedAt = null);

public sealed class ItemRefundHandler(ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ITransactionRepository transactions, IItemRefundRepository refunds, TransactionAggregateWriter aggregates,
    PersonalMoneyHandler personalMoney, Averio.Application.Recurring.IReimbursementRepository reimbursementClaims, IUnitOfWork uow, TimeProvider clock)
{
    private async Task<HouseholdMember> Member(Guid householdId, CancellationToken ct)
    {
        var id = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, id, ct) ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(x => x.UserId == id && x.LeftAtUtc == null);
    }
    private async Task<Transaction> Source(Guid h, Guid id, HouseholdMember member, bool write, CancellationToken ct)
    {
        var purchase = await transactions.GetVisibleByIdAsync(h, member.Id, id, ct) ?? throw new NotFoundException("transaction", id);
        var account = await accounts.GetVisibleByIdAsync(h, member.Id, purchase.AccountId, ct) ?? throw new NotFoundException("transaction", id);
        if (write && !TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        if (write && (account.Status != AccountStatus.Active || account.CutoverAtUtc is not null)) throw Invalid();
        return purchase;
    }
    public async Task<IReadOnlyList<ItemRefund>> ListAsync(Guid h, Guid id, CancellationToken ct)
    {
        var member = await Member(h, ct);
        await Source(h, id, member, false, ct);
        return await refunds.ListAsync(h, member.Id, id, ct);
    }
    public async Task<ItemRefund> OriginAsync(Guid h, Guid creditId, CancellationToken ct)
    {
        var member = await Member(h, ct);
        var credit = await Source(h, creditId, member, false, ct);
        if (credit.Kind != TransactionKind.Refund) throw new NotFoundException("refund", creditId);
        var refund = await refunds.ByCreditAsync(h, creditId, ct) ?? throw new NotFoundException("refund", creditId);
        await Source(h, refund.TransactionId, member, false, ct);
        return refund;
    }
    public async Task<Guid> RecordAsync(Guid h, Guid id, RecordItemRefund request, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(h, ct); // Serializes writes with all other household financial changes.
        var purchase = await Source(h, id, member, true, ct);
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new { id, request }))));
        var previous = await refunds.ByOperationAsync(h, request.OperationId, ct);
        if (previous is not null)
        {
            if (previous.CreatedByMemberId != member.Id || previous.RequestFingerprint != fingerprint) throw Changed();
            return previous.Id;
        }
        if (await reimbursementClaims.HasActivePaymentAsync(h, purchase.Id, ct))
            throw new BusinessRuleException("transaction_linked", "Error_TransactionLinked");
        if (purchase.UpdatedAt != request.ExpectedUpdatedAt) throw Changed();
        var item = purchase.Items.SingleOrDefault(x => x.Id == request.TransactionItemId);
        if (!request.MoneyReceived || purchase.Kind != TransactionKind.Expense || purchase.Status != TransactionStatus.Posted
            || purchase.IsArchived || item is null || request.Amount <= 0 || request.Amount > item.Amount
            || request.ReceivedOn < purchase.OccurredOn || request.ReceivedOn > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)
            || decimal.Round(request.Amount, 4) != request.Amount || request.OperationId == Guid.Empty)
            throw Invalid();
        Transaction credit;
        RefundIncomeSnapshot? originalIncome = null;
        if (request.CreditTransactionId is Guid creditId)
        {
            credit = await Source(h, creditId, member, true, ct);
            if (credit.UpdatedAt != request.ExpectedCreditUpdatedAt) throw Changed();
            if (credit.Kind != TransactionKind.Income || credit.Status != TransactionStatus.Posted || credit.IsArchived
                || credit.AccountId != purchase.AccountId || credit.Currency != purchase.Currency || credit.Visibility != purchase.Visibility
                || credit.Amount != request.Amount || credit.OccurredOn != request.ReceivedOn
                || await refunds.CreditHasDependenciesAsync(h, creditId, ct)) throw Invalid();
            originalIncome = RefundIncomeSnapshot.Capture(credit);
            await aggregates.ApplyAsync(credit, -1, ct);
            await transactions.StageItemPositionsAsync(credit, 1, ct);
            credit.ClassifyAsRefund(item, clock.GetUtcNow());
        }
        else
        {
            credit = Transaction.Create(h, purchase.AccountId, member.Id, TransactionKind.Refund, purchase.Visibility, purchase.Currency,
                purchase.Merchant, null, request.ReceivedOn,
                [new TransactionItemDraft(item.Description, item.CategoryId, item.CategoryName, item.CategoryIcon, request.Amount)], [], clock.GetUtcNow());
            transactions.Add(credit);
        }
        ItemRefund refund;
        try { refund = ItemRefund.Create(purchase, item.Id, credit, request.OperationId, member.Id, clock.GetUtcNow(), fingerprint, originalIncome); }
        catch (ArgumentException) { throw Invalid(); }
        await personalMoney.ApplyItemRefundWithinTransactionAsync(purchase, item, credit, ct);
        await aggregates.ApplyAsync(purchase, -1, ct);
        purchase.ApplyItemRefund(item.Id, refund.Amount, clock.GetUtcNow());
        refunds.Add(refund);
        await uow.SaveChangesAsync(ct);
        await aggregates.ApplyAsync(purchase, 1, ct);
        await tx.CommitAsync(ct);
        return refund.Id;
    }
    public async Task CancelAsync(Guid h, Guid purchaseId, Guid refundId, Guid version, DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(h, ct);
        var purchase = await Source(h, purchaseId, member, true, ct);
        var refund = await refunds.GetAsync(h, refundId, ct) ?? throw new NotFoundException("refund", refundId);
        if (refund.TransactionId != purchaseId) throw new NotFoundException("refund", refundId);
        if (refund.CancelledAt is not null) return;
        if (refund.Version != version || purchase.UpdatedAt != expectedUpdatedAt) throw Changed();
        var credit = await Source(h, refund.CreditTransactionId, member, true, ct);
        if (credit.Kind != TransactionKind.Refund || credit.Status != TransactionStatus.Posted) throw Invalid();
        if (refund.CreditOrigin == RefundCreditOrigin.Unknown)
            throw new BusinessRuleException("item_refund_origin_unknown", "Error_ItemRefundOriginUnknown");
        await aggregates.ApplyAsync(purchase, -1, ct);
        purchase.ApplyItemRefund(refund.TransactionItemId, -refund.Amount, clock.GetUtcNow());
        await personalMoney.ReverseTransactionChargeAsync(h, credit.Id, ct);
        if (refund.CreditOrigin == RefundCreditOrigin.ExistingIncome)
        {
            var snapshot = refund.ReadOriginalIncome();
            await transactions.StageItemPositionsAsync(credit, snapshot.Items.Length, ct);
            credit.RestoreRefundIncome(snapshot, clock.GetUtcNow());
        }
        else credit.Void(clock.GetUtcNow());
        refund.Cancel(member.Id, clock.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        await aggregates.ApplyAsync(purchase, 1, ct);
        if (refund.CreditOrigin == RefundCreditOrigin.ExistingIncome) await aggregates.ApplyAsync(credit, 1, ct);
        await tx.CommitAsync(ct);
    }
    private static BusinessRuleException Invalid() => new("item_refund_invalid", "Validation_ItemRefund");
    private static ConflictException Changed() => new("item_refund_changed", "Error_TransactionChanged");
}
