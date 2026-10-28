using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Common;
using Averio.Domain.Receipts;
using Averio.Domain.Transactions;

namespace Averio.Application.Receipts;

public sealed class PostReceiptHandler(ICurrentUser user, IHouseholdRepository households,
    IAccountRepository accounts, IReceiptRepository receipts, CreateTransactionHandler transactions,
    IUnitOfWork uow, TimeProvider clock)
{
    public async Task<Guid> HandleAsync(Guid householdId, Guid receiptId, Guid expectedVersion,
        string currency, decimal reviewedTotal, CreateTransactionCommand command, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        await using var tx = await uow.BeginTransactionAsync(ct);
        // The same household lock as ordinary ledger writes and departure. Never
        // trust an uploader/member/account identifier extracted from an image.
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        var member = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc == null);
        var scan = await receipts.GetAsync(householdId, member.Id, receiptId, ct)
            ?? throw new NotFoundException("receipt", receiptId);
        // Receipt identity is the posting idempotency key, including after expiry.
        if (scan.PostedTransactionId is Guid posted) return posted;
        if (scan.Status != ReceiptScanStatus.Ready || scan.Version != expectedVersion
            || scan.ExpiresAt <= clock.GetUtcNow() || scan.DocumentDeletedAt is not null)
            throw new ConflictException("receipt_changed", "Error_ReceiptChanged");
        if (command.HouseholdId != householdId || command.Kind != TransactionKind.Expense
            || !CurrencyCode.IsSupported(currency) || reviewedTotal <= 0
            || reviewedTotal > 999999999999999.9999m || decimal.Round(reviewedTotal, 4) != reviewedTotal
            || command.Items.Count is < 1 or > 100
            || command.Items.Any(x => x.Amount <= 0 || x.Amount > 999999999999999.9999m)
            || command.Items.Sum(x => x.Amount) != reviewedTotal)
            throw new BusinessRuleException("receipt_reconciliation", "Error_ReceiptReconciliation");
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, command.AccountId, ct)
            ?? throw new NotFoundException("account", command.AccountId);
        if (account.Currency.Value != currency)
            throw new BusinessRuleException("receipt_currency", "Error_ReceiptCurrency");

        var id = await transactions.CreateWithinTransactionAsync(command, userId, ct);
        // Compare-and-set also fences worker expiry/discard outside the household
        // lock. Failure rolls back ledger, aggregates, activity and allowance writes.
        if (!await receipts.PostAsync(scan.Id, expectedVersion, id, clock.GetUtcNow(), ct))
            throw new ConflictException("receipt_changed", "Error_ReceiptChanged");
        await tx.CommitAsync(ct);
        return id;
    }
}
