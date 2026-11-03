using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Common;
using Averio.Domain.Receipts;
using Averio.Domain.Transactions;

namespace Averio.Application.Receipts;

public sealed class PostReceiptHandler(ICurrentUser user, IHouseholdRepository households,
    IAccountRepository accounts, IReceiptRepository receipts, CreateTransactionHandler transactions,
    IUnitOfWork uow, TimeProvider clock, ReceiptCategorization? categorization = null)
{
    public async Task<Guid> HandleAsync(Guid householdId, Guid receiptId, Guid expectedVersion,
        string currency, decimal reviewedTotal, CreateTransactionCommand command, CancellationToken ct,
        IReadOnlyList<RememberReceiptCategory>? rememberCategoryRules = null)
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
        var selections = rememberCategoryRules ?? [];
        if (selections.Count > 100 || selections.Any(s => s is null))
            throw new BusinessRuleException("receipt_category_rule_invalid", "Error_ReceiptCategoryRuleInvalid");
        var fingerprint = ReceiptConfirmationFingerprint.Create(expectedVersion, currency, reviewedTotal, command, selections);
        // Only the original confirmation can be replayed, including after expiry
        // and subsequent ledger edits. Legacy rows cannot be reconstructed safely.
        if (scan.PostedTransactionId is Guid posted)
        {
            if (scan.ConfirmationFingerprint != fingerprint)
                throw new ConflictException("receipt_changed", "Error_ReceiptChanged");
            ReceiptMetrics.Confirmations.Add(1, new KeyValuePair<string, object?>("outcome", "replay"));
            return posted;
        }
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
        if (selections.Count > 0)
        {
            if (categorization is null) throw new InvalidOperationException("Receipt categorization is not registered.");
            var draft = scan.DraftJson is null ? null : System.Text.Json.JsonSerializer.Deserialize<ReceiptDraft>(scan.DraftJson);
            await categorization.RememberAsync(householdId, member.Id, draft, command, selections, clock.GetUtcNow(), ct);
        }
        // Compare-and-set also fences worker expiry/discard outside the household
        // lock. Failure rolls back ledger, aggregates, activity and allowance writes.
        if (!await receipts.PostAsync(scan.Id, expectedVersion, id, fingerprint, clock.GetUtcNow(), ct))
            throw new ConflictException("receipt_changed", "Error_ReceiptChanged");
        await tx.CommitAsync(ct);
        ReceiptMetrics.Confirmations.Add(1, new KeyValuePair<string, object?>("outcome", "posted"));
        return id;
    }
}

