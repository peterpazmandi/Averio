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

public enum PersonalTransferPurpose { Ordinary = 0, CreateAndFundGrant = 1, FundExistingGrant = 2 }

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
            entries.Where(x => x.Kind == PersonalMoneyEntryKind.PrivateCharge)
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
        if (operationId == Guid.Empty || amount <= 0 || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount)
            throw new BusinessRuleException("personal_money_amount_invalid", "Validation_TransactionAmountInvalid");
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
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
            if (previous.Kind != PersonalMoneyEntryKind.PrivateCharge || previous.TransactionId != transactionId
                || previous.Amount != -amount || previous.CorrectionExpectedVersion is not null
                || previous.CorrectionExpectedAmount is not null)
                throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
            await tx.CommitAsync(ct);
            return;
        }
        if (account.Scope != AccountScope.Personal || account.Visibility != AccountVisibility.Private
            || record.Visibility != TransactionVisibility.Private || record.Kind != TransactionKind.Expense
            || record.Status != TransactionStatus.Posted || amount > record.Amount)
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");

        if (wallet.Currency != record.Currency)
            throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
        if ((await repository.GetTransactionEntriesAsync(householdId, transactionId, ct))
            .Where(x => x.Kind == PersonalMoneyEntryKind.PrivateCharge).Sum(x => x.Amount) != 0)
            throw new ConflictException("personal_money_already_charged", "Error_PersonalMoneyAlreadyCharged");
        repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
            PersonalMoneyEntryKind.PrivateCharge, -amount, record.OccurredOn, clock.GetUtcNow(),
            transactionId: transactionId, operationId: operationId));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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
        var own = entries.Where(x => x.WalletId == wallet.Id && x.Kind == PersonalMoneyEntryKind.PrivateCharge).ToArray();
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
            if (previous.Kind != PersonalMoneyEntryKind.PrivateCharge || previous.TransactionId != transactionId
                || previous.Amount != correction || previous.CorrectionExpectedAmount != expectedAmount
                || previous.CorrectionExpectedVersion != expectedVersion)
                throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
            await tx.CommitAsync(ct);
            return;
        }
        if (own.Length != expectedVersion || -own.Sum(x => x.Amount) != expectedAmount)
            throw new ConflictException("personal_money_charge_changed", "Error_TransactionChanged");
        // Removing an existing link is allowed after account visibility changes.
        if (amount > 0 && (account.Scope != AccountScope.Personal || account.Visibility != AccountVisibility.Private
            || record.Visibility != TransactionVisibility.Private || record.Kind != TransactionKind.Expense
            || record.Status != TransactionStatus.Posted || wallet.Currency != record.Currency
            || amount - entries.Where(x => x.WalletId != wallet.Id).Sum(x => x.Amount) > record.Amount))
            throw new BusinessRuleException("personal_money_charge_not_allowed", "Error_PersonalMoneyChargeNotAllowed");
        if (correction != 0)
            repository.Add(PersonalMoneyEntry.Create(householdId, wallet.Id,
                PersonalMoneyEntryKind.PrivateCharge, correction, record.OccurredOn, clock.GetUtcNow(),
                transactionId: transactionId, operationId: operationId,
                correctionExpectedAmount: expectedAmount, correctionExpectedVersion: expectedVersion));
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // Called by TransferHandler within the SAME database transaction and account locks.
    public async Task LinkTransferAsync(Transfer transfer, HouseholdMember actor,
        Account source, Account destination, PersonalTransferPurpose purpose, CancellationToken ct)
    {
        if (purpose == PersonalTransferPurpose.Ordinary) return;
        if (!Enum.IsDefined(purpose)
            || source.Scope != AccountScope.Household || !source.IncludeInHouseholdReports
            || destination.Scope != AccountScope.Personal || destination.Visibility != AccountVisibility.Private
            || destination.IncludeInHouseholdReports || destination.Owners.Count != 1
            || !destination.Owners.Any(x => x.HouseholdMemberId == actor.Id)
            || source.Currency != destination.Currency)
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
            if (balance.FundingDifference < transfer.SentAmount)
                throw new BusinessRuleException("personal_money_insufficient_unfunded", "Error_PersonalMoneyInsufficientUnfunded");
        }
        repository.Add(PersonalMoneyEntry.Create(transfer.HouseholdId, wallet!.Id,
            PersonalMoneyEntryKind.Funding, transfer.SentAmount, transfer.OccurredOn,
            clock.GetUtcNow(), transferId: transfer.Id));
    }

    // Existing-transfer mutations never silently mint another grant.
    public async Task CorrectTransferAsync(Transfer transfer, HouseholdMember actor,
        Account source, Account destination, CancellationToken ct)
    {
        var entries = await repository.GetTransferEntriesAsync(transfer.HouseholdId, transfer.Id, ct);
        if (entries.Count == 0) return;
        var existingFunding = entries.Where(x => x.Kind == PersonalMoneyEntryKind.Funding).Sum(x => x.Amount);
        var createsGrant = entries.Any(x => x.Kind == PersonalMoneyEntryKind.Grant);
        // Account co-ownership never delegates authority over the original recipient's wallet.
        var wallet = await repository.GetWalletAsync(transfer.HouseholdId, actor.Id, ct);
        if (actor.LeftAtUtc is not null || wallet is null || entries.Any(x => x.WalletId != wallet.Id)
            || (createsGrant && actor.Role != HouseholdRole.Owner))
            throw new ForbiddenException();
        // A void may clean up a formerly eligible transfer after visibility/holder changes.
        if (transfer.VoidedAt is null && (source.Scope != AccountScope.Household
            || !source.IncludeInHouseholdReports || destination.Scope != AccountScope.Personal
            || destination.Visibility != AccountVisibility.Private || destination.IncludeInHouseholdReports
            || destination.Owners.Count != 1 || !destination.Owners.Any(x => x.HouseholdMemberId == actor.Id)
            || source.Currency != destination.Currency || wallet.Currency != source.Currency))
            throw new BusinessRuleException("personal_money_funding_invalid", "Error_PersonalMoneyFundingInvalid");
        if (!createsGrant && transfer.VoidedAt is null && transfer.SentAmount > existingFunding)
        {
            var walletId = entries.First().WalletId;
            var balance = PersonalMoneyBalance.FromEntries(await repository.GetEntriesAsync(walletId, ct));
            if (transfer.SentAmount - existingFunding > balance.FundingDifference)
                throw new BusinessRuleException("personal_money_insufficient_unfunded",
                    "Error_PersonalMoneyInsufficientUnfunded");
        }
        foreach (var group in entries.GroupBy(x => new { x.WalletId, x.Kind }))
        {
            var expected = transfer.VoidedAt is not null ? 0m : transfer.SentAmount;
            var originalTotals = group.GroupBy(x => x.OccurredOn)
                .ToDictionary(x => x.Key, x => x.Sum(e => e.Amount));
            var dates = originalTotals.Keys.Append(transfer.OccurredOn).Distinct();
            foreach (var date in dates)
            {
                var target = date == transfer.OccurredOn ? expected : 0m;
                var correction = target - originalTotals.GetValueOrDefault(date);
                if (correction == 0) continue;
                repository.Add(PersonalMoneyEntry.Create(transfer.HouseholdId, group.Key.WalletId,
                    group.Key.Kind, correction, date, clock.GetUtcNow(), transferId: transfer.Id));
            }
        }
    }

    // Source mutations retract an explicitly linked charge atomically; the user can relink the updated expense.
    public async Task ReverseTransactionChargeAsync(Guid householdId, Guid transactionId, CancellationToken ct)
    {
        var entries = await repository.GetTransactionEntriesAsync(householdId, transactionId, ct);
        foreach (var group in entries.Where(x => x.Kind == PersonalMoneyEntryKind.PrivateCharge)
                     .GroupBy(x => new { x.WalletId, x.OccurredOn }))
        {
            var total = group.Sum(x => x.Amount);
            if (total != 0m)
                repository.Add(PersonalMoneyEntry.Create(householdId, group.Key.WalletId,
                    PersonalMoneyEntryKind.PrivateCharge, -total, group.Key.OccurredOn,
                    clock.GetUtcNow(), transactionId: transactionId));
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
