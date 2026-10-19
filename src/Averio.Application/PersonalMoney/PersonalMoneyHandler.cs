using Averio.Application.Accounts.Common;
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

public sealed record PersonalMoneySnapshot(Guid WalletId, string Currency, decimal Available,
    decimal AttributedPrivateFunding, decimal FundingDifference,
    IReadOnlyList<PersonalMoneyEvent> Entries, IReadOnlyList<PersonalMoneyCharge> Charges);

public sealed record PersonalMoneyCharge(Guid TransactionId, decimal Amount, int Version);

public sealed record PersonalMoneyEvent(Guid Id, string Kind, decimal Amount, DateOnly OccurredOn);

public sealed class PersonalMoneyHandler(
    ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ITransactionRepository transactions, IPersonalMoneyRepository repository,
    IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<PersonalMoneySnapshot?> GetMineAsync(Guid householdId, CancellationToken ct)
    {
        var me = await MemberAsync(householdId, ct);
        var wallet = await repository.GetWalletAsync(householdId, me.Id, ct);
        if (wallet is null) return null;
        var entries = await repository.GetEntriesAsync(wallet.Id, ct);
        var balance = PersonalMoneyBalance.FromEntries(entries);
        return new PersonalMoneySnapshot(wallet.Id, wallet.Currency.Value, balance.Entitlement,
            balance.AttributedPrivateFunding, balance.FundingDifference,
            entries.OrderByDescending(x => x.OccurredOn).ThenByDescending(x => x.RecordedAt)
                .Select(x => new PersonalMoneyEvent(x.Id, x.Kind.ToString(), x.Amount, x.OccurredOn)).ToArray(),
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
                || previous.Amount != correction || previous.CorrectionExpectedAmount != expectedAmount
                || previous.CorrectionExpectedVersion != expectedVersion)
                throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
            await tx.CommitAsync(ct);
            return;
        }
        if (own.Length != expectedVersion || -own.Sum(x => x.Amount) != expectedAmount)
            throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
        // Removing an existing link is allowed after account visibility changes.
        var kind = own.First().Kind;
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
        if (correction != 0)
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
        var chargeKind = mine.First().Kind;
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
            new { x.WalletId, x.Kind, x.OccurredOn, x.TransactionId, x.RelatedTransactionId }))
        {
            var net = group.Sum(x => x.Amount);
            if (net == 0m) continue;
            repository.Add(PersonalMoneyEntry.Create(householdId, group.Key.WalletId,
                group.Key.Kind, -net, group.Key.OccurredOn, clock.GetUtcNow(),
                transactionId: group.Key.TransactionId,
                relatedTransactionId: group.Key.RelatedTransactionId));
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
