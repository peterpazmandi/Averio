using Averio.Application.Accounts.Common;
using Averio.Application.Transactions.Common;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.PersonalMoney;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;

namespace Averio.Application.PersonalMoney;

public enum PersonalTransferPurpose { Ordinary = 0, CreateAndFundGrant = 1, FundExistingGrant = 2, ReturnFunding = 3 }

public sealed record PersonalMoneySummary(Guid WalletId, string Currency, decimal Available,
    decimal AttributedPrivateFunding, decimal FundingDifference);

public sealed record PersonalMoneySnapshot(Guid WalletId, string Currency, decimal Available,
    decimal AttributedPrivateFunding, decimal FundingDifference,
    IReadOnlyList<PersonalMoneyEvent> Entries, IReadOnlyList<PersonalMoneyCharge> Charges);

public sealed record PersonalMoneyCharge(Guid TransactionId, decimal Amount, int Version);

public sealed record PersonalMoneyEvent(Guid Id, string Kind, decimal Amount, DateOnly OccurredOn,
    Guid? TransactionId, Guid? TransferId, Guid? RelatedTransactionId,
    Guid? RecurringAllowanceRuleId, string? TransactionName, Guid? TransactionItemId);

public sealed class PersonalMoneyHandler(
    ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ITransactionRepository transactions, IPersonalMoneyRepository repository,
    IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<PersonalMoneySummary?> GetMySummaryAsync(Guid householdId, CancellationToken ct)
    {
        var me = await MemberAsync(householdId, ct);
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct);
        if (wallet is null) return null;
        var balance = await repository.GetBalanceAsync(wallet.Id, ct);
        return new PersonalMoneySummary(wallet.Id, wallet.Currency.Value, balance.Entitlement,
            balance.AttributedPrivateFunding, balance.FundingDifference);
    }

    public async Task<PersonalMoneySnapshot?> GetMineAsync(Guid householdId, CancellationToken ct)
    {
        var me = await MemberAsync(householdId, ct);
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct);
        if (wallet is null) return null;
        var entries = await repository.GetEntriesAsync(wallet.Id, ct);
        var transactionIds = entries.Select(x => x.TransactionId ?? x.RelatedTransactionId)
            .OfType<Guid>().Distinct().ToArray();
        var transactionNames = await repository.GetTransactionNamesAsync(householdId, me.Id, transactionIds, ct);
        var balance = PersonalMoneyBalance.FromEntries(entries);
        return new PersonalMoneySnapshot(wallet.Id, wallet.Currency.Value, balance.Entitlement,
            balance.AttributedPrivateFunding, balance.FundingDifference,
            entries.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.RecordedAt)
                .Select(x => new PersonalMoneyEvent(x.Id, x.Kind.ToString(), x.Amount, x.OccurredOn,
                    x.TransactionId, x.TransferId, x.RelatedTransactionId,
                    x.RecurringAllowanceRuleId,
                    (x.TransactionId ?? x.RelatedTransactionId) is Guid transactionId
                        ? transactionNames.GetValueOrDefault(transactionId) : null,
                    x.TransactionItemId)).ToArray(),
            entries.Where(x => x.Kind is PersonalMoneyEntryKind.PrivateCharge or PersonalMoneyEntryKind.SharedCharge)
                .GroupBy(x => x.TransactionId!.Value).Where(g => g.Sum(x => x.Amount) < 0)
                .Select(g => new PersonalMoneyCharge(g.Key, -g.Sum(x => x.Amount), g.Count())).ToArray());
    }

    // The Owner approves discretionary household budget; no bank movement occurs.
    public async Task GrantAsync(Guid householdId, Guid recipientMemberId, string currency,
        decimal amount, DateOnly date, Guid operationId, CancellationToken ct)
    {
        ValidateAmount(amount, date);
        if (operationId == Guid.Empty || !CurrencyCode.IsSupported(currency))
            throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
        var denomination = CurrencyCode.From(currency);
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var actor = await MemberAsync(householdId, ct);
        if (actor.Role != HouseholdRole.Owner) throw new ForbiddenException();
        var household = await households.GetByIdForUserAsync(householdId, actor.UserId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.Members.Any(m => m.Id == recipientMemberId && m.LeftAtUtc is null))
            throw new NotFoundException("member", recipientMemberId);
        var wallet = await GetOrCreateAsync(householdId, recipientMemberId, denomination, ct);
        var previous = await repository.GetByOperationAsync(wallet.Id, operationId, ct);
        if (previous is not null)
        {
            if (previous.Kind != PersonalMoneyEntryKind.Grant || previous.Amount != amount
                || previous.OccurredOn != date)
                throw new ConflictException("personal_money_grant_conflict", "Error_PersonalMoneyAlreadyCharged");
            await tx.CommitAsync(ct);
            return;
        }
        repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
            PersonalMoneyEntryKind.Grant, amount, date, clock.GetUtcNow(), operationId: operationId));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // A private expense is explicitly linked by its owner; unlinked private spending is irrelevant.
    public async Task ChargePrivateAsync(Guid householdId, Guid transactionId,
        decimal amount, Guid operationId, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        await ChargeWithinTransactionAsync(householdId, transactionId, amount, operationId, ct);
        await tx.CommitAsync(ct);
    }

    // Caller owns the transaction; creation and attribution must commit or roll back together.
    internal async Task ChargeWithinTransactionAsync(Guid householdId, Guid transactionId,
        decimal amount, Guid operationId, CancellationToken ct)
    {
        if (operationId == Guid.Empty || amount <= 0 || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount)
            throw new BusinessRuleException("personal_money_amount_invalid", "Validation_TransactionAmountInvalid");

        var me = await MemberAsync(householdId, ct);
        var record = await transactions.GetVisibleByIdAsync(householdId, me.Id, transactionId, ct)
            ?? throw new NotFoundException("transaction", transactionId);
        var account = await accounts.GetVisibleByIdAsync(householdId, me.Id, record.AccountId, ct)
            ?? throw new NotFoundException("account", record.AccountId);
        if (!AccountAccess.CanModify(account, me)) throw new ForbiddenException();
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct)
            ?? throw new BusinessRuleException("personal_money_not_configured", "Error_PersonalMoneyNotConfigured");
        // Creation receipts survive unlink, source edits and voids. A retry never replays spending.
        var previous = await repository.GetByOperationAsync(wallet.Id, operationId, ct);
        if (previous is not null)
        {
            if (!IsCharge(previous) || previous.TransactionId != transactionId
                || previous.Amount != -amount || previous.CorrectionExpectedVersion is not null
                || previous.CorrectionExpectedAmount is not null)
                throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
            return;
        }
        var kind = ChargeKind(account, record);
        if (kind is null || record.Status != TransactionStatus.Posted || account.Status != AccountStatus.Active
            || amount > record.Amount)
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");

        if (wallet.Currency != record.Currency)
            throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
        var sourceEntries = await repository.GetTransactionEntriesAsync(householdId, transactionId, ct);
        // Whole-transaction linking remains for historical purchases, but must not
        // be mixed with per-item attribution (which has its own correction rules).
        if (sourceEntries.Where(x => IsCharge(x) && x.TransactionItemId is not null)
            .GroupBy(x => new { x.WalletId, x.TransactionItemId }).Any(g => g.Sum(x => x.Amount) != 0m))
            throw new ConflictException("personal_money_already_charged", "Error_PersonalMoneyAlreadyCharged");
        var alreadyCharged = -sourceEntries.Where(IsCharge).Sum(x => x.Amount);
        var ownCharge = -sourceEntries.Where(x => x.WalletId == wallet.Id && IsCharge(x)).Sum(x => x.Amount);
        if (ownCharge > 0 || alreadyCharged + amount > record.Amount)
            throw new ConflictException("personal_money_already_charged", "Error_PersonalMoneyAlreadyCharged");
        repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
            kind.Value, -amount, record.OccurredOn, clock.GetUtcNow(),
            transactionId: transactionId, operationId: operationId));
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Household lock serializes corrections with source edits/voids and membership changes.
    // Version counts all source events, so even a change-away-and-back is detected.
    public async Task CorrectChargeAsync(Guid householdId, Guid transactionId, decimal amount,
        decimal expectedAmount, int expectedVersion, Guid operationId, CancellationToken ct)
    {
        if (amount < 0 || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount
            || expectedAmount <= 0 || expectedAmount > 999999999999999.9999m
            || decimal.Round(expectedAmount, 4) != expectedAmount || expectedVersion < 1 || operationId == Guid.Empty)
            throw new BusinessRuleException("personal_money_amount_invalid", "Validation_TransactionAmountInvalid");
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var me = await MemberAsync(householdId, ct);
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct)
            ?? throw new NotFoundException("charge", transactionId);
        var entries = await repository.GetTransactionEntriesAsync(householdId, transactionId, ct);
        var own = entries.Where(x => x.WalletId == wallet.Id && IsCharge(x)).ToArray();
        var hasItemCharges = own.Where(x => x.TransactionItemId is not null).GroupBy(x => x.TransactionItemId)
            .Any(g => g.Sum(x => x.Amount) != 0m);
        if (hasItemCharges && amount != 0)
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");
        if (own.Length == 0) throw new NotFoundException("charge", transactionId);
        var record = await transactions.GetVisibleByIdAsync(householdId, me.Id, transactionId, ct)
            ?? throw new NotFoundException("transaction", transactionId);
        var account = await accounts.GetVisibleByIdAsync(householdId, me.Id, record.AccountId, ct)
            ?? throw new NotFoundException("account", record.AccountId);
        if (!AccountAccess.CanModify(account, me)) throw new ForbiddenException();
        var correction = expectedAmount - amount;
        var previous = await repository.GetByOperationAsync(wallet.Id, operationId, ct);
        if (previous is not null)
        {
            if (!IsCharge(previous) || previous.TransactionId != transactionId
                || (previous.TransactionItemId is not null ? amount != 0 : previous.Amount != correction)
                || previous.CorrectionExpectedAmount != expectedAmount
                || previous.CorrectionExpectedVersion != expectedVersion)
                throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
            await tx.CommitAsync(ct);
            return;
        }
        if (own.Length != expectedVersion || -own.Sum(x => x.Amount) != expectedAmount)
            throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
        // Removing an existing link is allowed after account visibility changes.
        var kind = own.GroupBy(x => x.Kind)
            .Where(g => g.Sum(x => x.Amount) < 0)
            .Select(g => g.Key).Single();
        if (amount > 0 && (ChargeKind(account, record) != kind
            || record.Status != TransactionStatus.Posted || account.Status != AccountStatus.Active
            || wallet.Currency != record.Currency
            || amount - entries.Where(x => x.WalletId != wallet.Id && IsCharge(x)).Sum(x => x.Amount) > record.Amount))
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");
        var purchaseRefunds = await repository.GetRefundsOfPurchaseAsync(householdId, transactionId, ct);
        var activeRefundTotal = purchaseRefunds.Where(x => x.WalletId == wallet.Id && IsRefund(x))
            .Sum(x => x.Amount);
        if (amount < activeRefundTotal)
        {
            // Existing linked refunds would exceed the corrected purchase allocation.
            // Retract the refund budget attributions without modifying bank income transactions.
            foreach (var refundGroup in purchaseRefunds
                .Where(x => x.WalletId == wallet.Id && IsRefund(x))
                .GroupBy(x => new { x.Kind, x.TransactionId, x.OccurredOn, x.RelatedTransactionId }))
            {
                var net = refundGroup.Sum(x => x.Amount);
                if (net == 0m) continue;
                repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
                    refundGroup.Key.Kind, -net, refundGroup.Key.OccurredOn, clock.GetUtcNow(),
                    transactionId: refundGroup.Key.TransactionId,
                    relatedTransactionId: refundGroup.Key.RelatedTransactionId));
            }
        }
        if (hasItemCharges)
        {
            var first = true;
            foreach (var group in own.GroupBy(x => new { x.Kind, x.OccurredOn, x.TransactionItemId }))
            {
                var net = group.Sum(x => x.Amount);
                if (net == 0) continue;
                repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
                    group.Key.Kind, -net, group.Key.OccurredOn, clock.GetUtcNow(),
                    transactionId: transactionId, transactionItemId: group.Key.TransactionItemId,
                    operationId: first ? operationId : null,
                    correctionExpectedAmount: first ? expectedAmount : null,
                    correctionExpectedVersion: first ? expectedVersion : null));
                first = false;
            }
            record.MarkAllocationChanged(clock.GetUtcNow());
        }
        else if (correction != 0)
            repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
                kind, correction, record.OccurredOn, clock.GetUtcNow(),
                transactionId: transactionId, operationId: operationId,
                correctionExpectedAmount: expectedAmount, correctionExpectedVersion: expectedVersion));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // A refund is an existing posted Income record explicitly matched to a previously charged expense.
    public async Task RefundAsync(Guid householdId, Guid refundTransactionId, Guid purchaseId,
        decimal amount, Guid operationId, CancellationToken ct)
    {
        ValidateAmount(amount, new DateOnly(2000, 1, 1));
        if (operationId == Guid.Empty) throw new BusinessRuleException("personal_money_amount_invalid",
            "Validation_TransactionAmountInvalid");
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var me = await MemberAsync(householdId, ct);
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct)
            ?? throw new BusinessRuleException("personal_money_not_configured", "Error_PersonalMoneyNotConfigured");
        var previous = await repository.GetByOperationAsync(wallet.Id, operationId, ct);
        if (previous is not null)
        {
            if (previous.TransactionId != refundTransactionId || previous.RelatedTransactionId != purchaseId
                || previous.Amount != amount || previous.Kind is not (PersonalMoneyEntryKind.PrivateRefund or PersonalMoneyEntryKind.SharedRefund))
                throw new ConflictException("personal_money_refund_changed", "Error_TransactionChanged");
            await tx.CommitAsync(ct);
            return;
        }
        var purchase = await transactions.GetVisibleByIdAsync(householdId, me.Id, purchaseId, ct)
            ?? throw new NotFoundException("transaction", purchaseId);
        var refund = await transactions.GetVisibleByIdAsync(householdId, me.Id, refundTransactionId, ct)
            ?? throw new NotFoundException("transaction", refundTransactionId);
        var account = await accounts.GetVisibleByIdAsync(householdId, me.Id, purchase.AccountId, ct)
            ?? throw new NotFoundException("account", purchase.AccountId);
        if (!AccountAccess.CanModify(account, me)) throw new ForbiddenException();
        var charge = await repository.GetTransactionEntriesAsync(householdId, purchaseId, ct);
        var mine = charge.Where(x => x.WalletId == wallet.Id && IsCharge(x)).ToArray();
        var charged = -mine.Sum(x => x.Amount);
        var refundEntries = await repository.GetTransactionEntriesAsync(householdId, refundTransactionId, ct);
        var previousRefunds = await repository.GetRefundsOfPurchaseAsync(householdId, purchaseId, ct);
        if (charged <= 0 || refundEntries.Where(IsRefund).Sum(x => x.Amount) != 0m
            || previousRefunds.Where(x => x.WalletId == wallet.Id).Sum(x => x.Amount) + amount > charged
            || refund.AccountId != purchase.AccountId || refund.Kind != TransactionKind.Income
            || refund.Status != TransactionStatus.Posted || refund.Amount < amount
            || purchase.Kind != TransactionKind.Expense || purchase.Status != TransactionStatus.Posted
            || wallet.Currency != refund.Currency || wallet.Currency != purchase.Currency
            || account.Status != AccountStatus.Active)
            throw new BusinessRuleException("personal_money_refund_invalid", "Error_PersonalMoneyRefundInvalid");
        var activeChargeKind = mine.GroupBy(x => x.Kind)
            .Where(g => g.Sum(x => x.Amount) < 0).Select(g => (PersonalMoneyEntryKind?)g.Key)
            .SingleOrDefault();
        if (activeChargeKind is null)
            throw new BusinessRuleException("personal_money_refund_invalid", "Error_PersonalMoneyRefundInvalid");
        var chargeKind = activeChargeKind.Value;
        var refundKind = chargeKind == PersonalMoneyEntryKind.PrivateCharge
            ? PersonalMoneyEntryKind.PrivateRefund : PersonalMoneyEntryKind.SharedRefund;
        if (ChargeKind(account, purchase) != chargeKind
            || refund.Visibility != purchase.Visibility)
            throw new BusinessRuleException("personal_money_refund_invalid", "Error_PersonalMoneyRefundInvalid");
        repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id, refundKind,
            amount, refund.OccurredOn, clock.GetUtcNow(), transactionId: refundTransactionId,
            operationId: operationId, relatedTransactionId: purchaseId));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task SurrenderAsync(Guid householdId, decimal amount,
        DateOnly date, Guid operationId, CancellationToken ct)
    {
        ValidateAmount(amount, date);
        if (operationId == Guid.Empty)
            throw new BusinessRuleException("personal_money_amount_invalid", "Validation_TransactionAmountInvalid");
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var me = await MemberAsync(householdId, ct);
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct)
            ?? throw new BusinessRuleException("personal_money_not_configured", "Error_PersonalMoneyNotConfigured");
        var previous = await repository.GetByOperationAsync(wallet.Id, operationId, ct);
        if (previous is not null)
        {
            if (previous.Kind != PersonalMoneyEntryKind.Surrender || previous.Amount != -amount
                || previous.OccurredOn != date)
                throw new ConflictException("personal_money_surrender_changed", "Error_TransactionChanged");
            await tx.CommitAsync(ct);
            return;
        }
        var balance = PersonalMoneyBalance.FromEntries(await repository.GetEntriesAsync(wallet.Id, ct));
        if (balance.Entitlement < amount)
            throw new BusinessRuleException("personal_money_surrender_exceeds_available", "Error_PersonalMoneySurrenderInvalid");
        repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
            PersonalMoneyEntryKind.Surrender, -amount, date, clock.GetUtcNow(), operationId: operationId));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static bool IsCharge(PersonalMoneyEntry x) => x.Kind is
        PersonalMoneyEntryKind.PrivateCharge or PersonalMoneyEntryKind.SharedCharge;

    private static bool IsRefund(PersonalMoneyEntry x) => x.Kind is
        PersonalMoneyEntryKind.PrivateRefund or PersonalMoneyEntryKind.SharedRefund;

    private static PersonalMoneyEntryKind? ChargeKind(Account account, Transaction record)
    {
        if (record.Kind != TransactionKind.Expense) return null;
        if (account.Scope == AccountScope.Personal && account.Visibility == AccountVisibility.Private
            && record.Visibility == TransactionVisibility.Private && !account.IncludeInHouseholdReports)
            return PersonalMoneyEntryKind.PrivateCharge;
        if (account.Scope == AccountScope.Household && account.Visibility == AccountVisibility.Household
            && record.Visibility == TransactionVisibility.Household && account.IncludeInHouseholdReports)
            return PersonalMoneyEntryKind.SharedCharge;
        return null;
    }

    // Called by TransferHandler within the SAME database transaction and account locks.
    public async Task LinkTransferAsync(Transfer transfer, HouseholdMember actor,
        Account source, Account destination, PersonalTransferPurpose purpose, CancellationToken ct)
    {
        if (purpose == PersonalTransferPurpose.Ordinary) return;
        if (!Enum.IsDefined(purpose))
            throw new BusinessRuleException("personal_money_purpose_invalid", "Error_PersonalMoneyPurposeInvalid");
        var isReturn = purpose == PersonalTransferPurpose.ReturnFunding;
        if (!EligibleFundingPair(source, destination, actor, isReturn))
            throw new BusinessRuleException("personal_money_funding_invalid", "Error_PersonalMoneyFundingInvalid");

        var wallet = await repository.GetWalletAsync(transfer.HouseholdId, actor.Id, ct);
        if (purpose == PersonalTransferPurpose.CreateAndFundGrant)
        {
            if (actor.Role != HouseholdRole.Owner) throw new ForbiddenException();
            wallet ??= await GetOrCreateAsync(transfer.HouseholdId, actor.Id, source.Currency, ct);
            if (wallet.Currency != source.Currency)
                throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
            repository.Add(PersonalMoneyEntry.Create(transfer.HouseholdId, wallet.Id,
                PersonalMoneyEntryKind.Grant, transfer.SentAmount, transfer.OccurredOn,
                clock.GetUtcNow(), transferId: transfer.Id));
        }
        else
        {
            if (wallet is null || wallet.Currency != source.Currency)
                throw new BusinessRuleException("personal_money_grant_required", "Error_PersonalMoneyGrantRequired");
            var balance = PersonalMoneyBalance.FromEntries(await repository.GetEntriesAsync(wallet.Id, ct));
            if (isReturn && balance.AttributedPrivateFunding < transfer.SentAmount
                || !isReturn && balance.FundingDifference < transfer.SentAmount)
                throw new BusinessRuleException("personal_money_insufficient_unfunded",
                    "Error_PersonalMoneyInsufficientUnfunded");
        }
        repository.Add(PersonalMoneyEntry.Create(transfer.HouseholdId, wallet!.Id,
            PersonalMoneyEntryKind.Funding, isReturn ? -transfer.SentAmount : transfer.SentAmount,
            transfer.OccurredOn, clock.GetUtcNow(), transferId: transfer.Id));
    }

    // Retain the original sign of the transfer purpose across edits and voids.
    public async Task CorrectTransferAsync(Transfer transfer, HouseholdMember actor,
        Account source, Account destination, CancellationToken ct)
    {
        var entries = await repository.GetTransferEntriesAsync(transfer.HouseholdId, transfer.Id, ct);
        if (entries.Count == 0) return;
        var originalFunding = entries.Where(x => x.Kind == PersonalMoneyEntryKind.Funding)
            .OrderBy(x => x.RecordedAt).ThenBy(x => x.Id).First();
        var isReturn = originalFunding.Amount < 0;
        var currentFunding = entries.Where(x => x.Kind == PersonalMoneyEntryKind.Funding).Sum(x => x.Amount);
        var createsGrant = entries.Any(x => x.Kind == PersonalMoneyEntryKind.Grant);
        var wallet = await repository.GetWalletAsync(transfer.HouseholdId, actor.Id, ct);
        if (actor.LeftAtUtc is not null || wallet is null || entries.Any(x => x.WalletId != wallet.Id)
            || (createsGrant && actor.Role != HouseholdRole.Owner))
            throw new ForbiddenException();
        if (transfer.VoidedAt is null &&
            (!EligibleFundingPair(source, destination, actor, isReturn) || wallet.Currency != source.Currency))
            throw new BusinessRuleException("personal_money_funding_invalid", "Error_PersonalMoneyFundingInvalid");
        if (transfer.VoidedAt is null && !createsGrant)
        {
            var balance = PersonalMoneyBalance.FromEntries(await repository.GetEntriesAsync(wallet.Id, ct));
            var additionalFunding = isReturn
                ? transfer.SentAmount + currentFunding // currentFunding negative: delta in return
                : transfer.SentAmount - currentFunding;
            if (additionalFunding > 0 && (isReturn
                ? balance.AttributedPrivateFunding < additionalFunding
                : balance.FundingDifference < additionalFunding))
                throw new BusinessRuleException("personal_money_insufficient_unfunded",
                    "Error_PersonalMoneyInsufficientUnfunded");
        }
        foreach (var group in entries.GroupBy(x => new { x.WalletId, x.Kind }))
        {
            var expected = transfer.VoidedAt is not null ? 0m
                : isReturn && group.Key.Kind == PersonalMoneyEntryKind.Funding ? -transfer.SentAmount
                : transfer.SentAmount;
            var totalsByDate = group.GroupBy(x => x.OccurredOn)
                .ToDictionary(x => x.Key, x => x.Sum(e => e.Amount));
            foreach (var date in totalsByDate.Keys.Append(transfer.OccurredOn).Distinct())
            {
                var target = date == transfer.OccurredOn ? expected : 0m;
                var correction = target - totalsByDate.GetValueOrDefault(date);
                if (correction == 0) continue;
                repository.Add(PersonalMoneyEntry.Create(transfer.HouseholdId, group.Key.WalletId,
                    group.Key.Kind, correction, date, clock.GetUtcNow(), transferId: transfer.Id));
            }
        }
    }

    private static bool EligibleFundingPair(Account source, Account destination,
        HouseholdMember member, bool returning)
    {
        var householdAccount = returning ? destination : source;
        var privateAccount = returning ? source : destination;
        return householdAccount.Scope == AccountScope.Household
            && householdAccount.IncludeInHouseholdReports
            && privateAccount.Scope == AccountScope.Personal
            && privateAccount.Visibility == AccountVisibility.Private
            && !privateAccount.IncludeInHouseholdReports
            && privateAccount.Owners.Count == 1
            && privateAccount.Owners.Any(x => x.HouseholdMemberId == member.Id)
            && source.Currency == destination.Currency;
    }

    // This is always called from an enclosing household-locked transaction.
    // The ordinary shared transaction API never serializes these member-private links.
    internal async Task ApplyItemChargesWithinTransactionAsync(Guid householdId,
        Guid transactionId, IReadOnlyList<TransactionItemInput> inputs, CancellationToken ct)
    {
        if (!inputs.Any(x => x.PersonalMoneyAmount is not null)) return;
        var member = await MemberAsync(householdId, ct);
        var record = await transactions.GetVisibleByIdAsync(householdId, member.Id, transactionId, ct)
            ?? throw new NotFoundException("transaction", transactionId);
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, record.AccountId, ct)
            ?? throw new NotFoundException("account", record.AccountId);
        var chargeKind = ChargeKind(account, record);
        if (!AccountAccess.CanModify(account, member)
            || (chargeKind is null && inputs.Any(x => x.PersonalMoneyAmount > 0))
            || record.Kind != TransactionKind.Expense || record.Status != TransactionStatus.Posted)
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");
        var entries = await repository.GetTransactionEntriesAsync(householdId, transactionId, ct);
        var currentItems = record.Items.OrderBy(x => x.Position).ToArray();
        if (currentItems.Length != inputs.Count)
            throw new ConflictException("transaction_changed", "Error_TransactionChanged");

        var targets = inputs.Select((input, i) => new
        {
            Item = currentItems[i],
            Amount = input.PersonalMoneyAmount,
        }).Where(x => x.Amount is not null).ToArray();
        if (targets.Length == 0) return;
        var wallet = await repository.GetWalletAsync(householdId, member.Id, ct);
        if (wallet is null && targets.All(x => x.Amount == 0)) return;
        if (wallet is null)
            throw new BusinessRuleException("personal_money_not_configured", "Error_PersonalMoneyNotConfigured");
        if (wallet.Currency != record.Currency)
            throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");

        // A legacy whole-receipt charge cannot be silently overlaid with item-level
        // allocations. The holder must unlink the legacy attribution first.
        if (targets.Any(x => x.Amount > 0)
            && entries.Where(x => IsCharge(x) && x.TransactionItemId is null)
                .GroupBy(x => x.WalletId).Any(g => g.Sum(x => x.Amount) != 0m))
            throw new ConflictException("transaction_changed", "Error_TransactionChanged");
        var projectedTotal = -entries.Where(IsCharge).Sum(x => x.Amount);
        var projectedOwnTotal = -entries.Where(x => IsCharge(x) && x.WalletId == wallet.Id).Sum(x => x.Amount);
        foreach (var target in targets)
        {
            var amount = target.Amount!.Value;
            if (amount < 0 || amount > target.Item.Amount || decimal.Round(amount, 4) != amount)
                throw new BusinessRuleException("personal_money_amount_invalid", "Validation_TransactionAmountInvalid");
            var onItem = entries.Where(x => x.TransactionItemId == target.Item.Id && IsCharge(x)).ToArray();
            var own = onItem.Where(x => x.WalletId == wallet.Id).ToArray();
            var current = -own.Sum(x => x.Amount);
            var others = -onItem.Where(x => x.WalletId != wallet.Id).Sum(x => x.Amount);
            // Allocating the *same* receipt item to multiple members requires a
            // separate mutual-authorization policy, deferred by ADR-032.
            if (others > 0m && amount > 0m || amount + others > target.Item.Amount)
                throw new ConflictException("transaction_changed", "Error_TransactionChanged");
            var difference = current - amount;
            if (difference != 0)
            {
                if (amount == 0)
                {
                    // Unlink against the original classification even after account settings change.
                    foreach (var group in own.GroupBy(x => new { x.Kind, x.OccurredOn }))
                    {
                        var net = group.Sum(x => x.Amount);
                        if (net != 0)
                            repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
                                group.Key.Kind, -net, group.Key.OccurredOn, clock.GetUtcNow(),
                                transactionId: transactionId, transactionItemId: target.Item.Id));
                    }
                }
                else
                    repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
                        chargeKind!.Value, difference, record.OccurredOn, clock.GetUtcNow(),
                        transactionId: transactionId, operationId: Guid.NewGuid(),
                        transactionItemId: target.Item.Id));
            }
            projectedTotal += amount - current;
            projectedOwnTotal += amount - current;
        }
        if (projectedTotal > record.Amount)
            throw new ConflictException("transaction_changed", "Error_TransactionChanged");
        await ReconcilePurchaseRefundsAsync(householdId, transactionId,
            new Dictionary<Guid, decimal> { [wallet.Id] = projectedOwnTotal }, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    // Reconcile source edits without divulging another member's item allocations to the actor.
    // Removed items are reversed; reductions that would confiscate another member's allowance
    // are rejected with the same generic stale-transaction conflict as other concurrent edits.
    internal async Task ReconcileItemSourceChangesAsync(Guid householdId, Transaction transaction,
        IReadOnlyList<TransactionItemDraft> proposed, IReadOnlyList<TransactionItemInput> inputs,
        Guid targetAccountId,
        TransactionKind targetKind, TransactionVisibility targetVisibility,
        DateOnly targetDate, CancellationToken ct)
    {
        var entries = await repository.GetTransactionEntriesAsync(householdId, transaction.Id, ct);
        var active = entries.Where(IsCharge).GroupBy(x => new
            { x.WalletId, x.Kind, x.OccurredOn, x.TransactionItemId })
            .Select(g => new { g.Key, Net = g.Sum(x => x.Amount) })
            .Where(x => x.Net != 0).ToArray();
        var changedSource = transaction.AccountId != targetAccountId
            || transaction.Kind != targetKind || transaction.Visibility != targetVisibility
            || transaction.OccurredOn != targetDate;
        var retained = proposed.Where(x => x.ExistingItemId is not null)
            .ToDictionary(x => x.ExistingItemId!.Value);
        var changedItems = transaction.Items.Count != proposed.Count
            || transaction.Items.Any(item => !retained.TryGetValue(item.Id, out var p)
                || item.Amount != p.Amount);
        // A refund income has no charge entries of its own. Financial edits must
        // still retract its wallet credit; metadata-only edits preserve it.
        if (entries.Any(IsRefund) && (changedSource || changedItems))
            await ReverseTransactionChargeAsync(householdId, transaction.Id, ct);
        if (active.Length == 0) return;
        var hasItemLinks = active.Any(x => x.Key.TransactionItemId is not null);
        if (hasItemLinks && changedSource)
            throw new ConflictException("transaction_changed", "Error_TransactionChanged");
        // Legacy whole-receipt charges are not item-linked; financial source edits
        // keep the old behavior of reversing the charge and dependent refunds.
        if (!hasItemLinks && (changedSource || changedItems))
        {
            await ReverseTransactionChargeAsync(householdId, transaction.Id, ct);
            return;
        }

        var newInputs = inputs.Where(x => x.ExistingItemId is not null)
            .ToDictionary(x => x.ExistingItemId!.Value);
        var actor = await MemberAsync(householdId, ct);
        var ownWallet = await repository.GetWalletAsync(householdId, actor.Id, ct);
        foreach (var item in transaction.Items)
        {
            var linked = active.Where(x => x.Key.TransactionItemId == item.Id).ToArray();
            if (linked.Length == 0) continue;
            if (!retained.TryGetValue(item.Id, out var proposedItem))
            {
                foreach (var entry in linked)
                    repository.Add(PersonalMoneyEntry.Create(householdId, entry.Key.WalletId,
                        entry.Key.Kind, -entry.Net, entry.Key.OccurredOn, clock.GetUtcNow(),
                        transactionId: transaction.Id, transactionItemId: item.Id));
            }
            else
            {
                var own = -linked.Where(x => ownWallet is not null && x.Key.WalletId == ownWallet.Id)
                    .Sum(x => x.Net);
                var other = -linked.Where(x => ownWallet is null || x.Key.WalletId != ownWallet.Id)
                    .Sum(x => x.Net);
                var requested = newInputs[item.Id].PersonalMoneyAmount ?? own;
                if (other + requested > proposedItem.Amount)
                    throw new ConflictException("transaction_changed", "Error_TransactionChanged");
            }
        }
        // Mixed legacy and item-level links share the source. If the monetary
        // amount changes, the legacy charge cannot silently retain old coverage.
        var legacy = active.Where(x => x.Key.TransactionItemId is null).ToArray();
        if (legacy.Length > 0 && changedItems)
            throw new ConflictException("transaction_changed", "Error_TransactionChanged");
        var remaining = active.GroupBy(x => x.Key.WalletId).ToDictionary(g => g.Key,
            g => -g.Where(x => x.Key.TransactionItemId is null
                || retained.ContainsKey(x.Key.TransactionItemId.Value)).Sum(x => x.Net));
        // Explicit own allocations are applied after source persistence. Assess their
        // final combined coverage there, including replacement/new receipt items.
        if (ownWallet is not null && inputs.Any(x => x.PersonalMoneyAmount is not null))
            remaining.Remove(ownWallet.Id);
        await ReconcilePurchaseRefundsAsync(householdId, transaction.Id, remaining, ct);
    }

    // Refunds are purchase-level credits. Keep them while the holder's remaining
    // allocation covers them; otherwise retract that holder's refund attribution,
    // retaining the real bank income and immutable audit history.
    private async Task ReconcilePurchaseRefundsAsync(Guid householdId, Guid purchaseId,
        IReadOnlyDictionary<Guid, decimal> remainingCharges, CancellationToken ct)
    {
        var refunds = await repository.GetRefundsOfPurchaseAsync(householdId, purchaseId, ct);
        foreach (var wallet in refunds.Where(IsRefund).GroupBy(x => x.WalletId))
        {
            if (!remainingCharges.TryGetValue(wallet.Key, out var remaining)
                || wallet.Sum(x => x.Amount) <= remaining) continue;
            foreach (var group in wallet.GroupBy(x => new
                { x.Kind, x.TransactionId, x.RelatedTransactionId, x.OccurredOn }))
            {
                var net = group.Sum(x => x.Amount);
                if (net == 0m) continue;
                repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Key,
                    group.Key.Kind, -net, group.Key.OccurredOn, clock.GetUtcNow(),
                    transactionId: group.Key.TransactionId,
                    relatedTransactionId: group.Key.RelatedTransactionId));
            }
        }
    }

    // Source mutations retract an explicitly linked charge atomically; the user can relink the updated expense.
    public async Task ReverseTransactionChargeAsync(Guid householdId, Guid transactionId, CancellationToken ct)
    {
        // A source edit/void invalidates its own charged/refunded budget attribution.
        // Editing/voiding the original purchase must also retract dependent refunds.
        var entries = await repository.GetTransactionEntriesAsync(householdId, transactionId, ct);
        var dependents = await repository.GetRefundsOfPurchaseAsync(householdId, transactionId, ct);
        var all = entries.Where(x => IsCharge(x) || IsRefund(x))
            .Concat(dependents.Where(IsRefund)).DistinctBy(x => x.Id);
        foreach (var group in all.GroupBy(x =>
            new { x.WalletId, x.Kind, x.OccurredOn, x.TransactionId, x.RelatedTransactionId, x.TransactionItemId }))
        {
            var net = group.Sum(x => x.Amount);
            if (net == 0m) continue;
            repository.Add(PersonalMoneyEntry.Create(householdId, group.Key.WalletId,
                group.Key.Kind, -net, group.Key.OccurredOn, clock.GetUtcNow(),
                transactionId: group.Key.TransactionId,
                relatedTransactionId: group.Key.RelatedTransactionId,
                transactionItemId: group.Key.TransactionItemId));
        }
    }

    private async Task<PersonalMoneyWallet> GetOrCreateAsync(Guid householdId,
        Guid memberId, CurrencyCode currency, CancellationToken ct)
    {
        var wallet = await repository.GetWalletAsync(householdId, memberId, ct);
        if (wallet is not null)
        {
            if (wallet.Currency != currency)
                throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
            return wallet;
        }
        wallet = PersonalMoneyWallet.Create(householdId, memberId, currency, clock.GetUtcNow());
        repository.Add(wallet);
        return wallet;
    }

    private async Task<HouseholdMember> MemberAsync(Guid householdId, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
    }

    private static void ValidateAmount(decimal amount, DateOnly date)
    {
        if (amount <= 0 || amount > 999999999999999.9999m
            || decimal.Round(amount, 4) != amount || date == default)
            throw new BusinessRuleException("personal_money_amount_invalid", "Validation_TransactionAmountInvalid");
    }
}
