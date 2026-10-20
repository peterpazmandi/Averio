using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.PersonalMoney;

namespace Averio.Application.PersonalMoney;

public sealed record RecurringAllowanceInput(Guid RecipientMemberId, string Currency,
    decimal Amount, DateOnly StartsOn, DateOnly? EndsOn, Guid OperationId);
public sealed record RecurringAllowanceUpdate(decimal Amount, DateOnly? EndsOn,
    bool IsPaused, bool IsStopped, Guid ExpectedVersion);
public sealed record RecurringAllowanceView(Guid Id, Guid RecipientMemberId, string Currency,
    decimal Amount, DateOnly StartsOn, DateOnly? EndsOn, DateOnly? NextDueOn,
    bool IsPaused, bool IsStopped, bool IsBlocked, Guid Version);
public sealed record RecurringAllowanceReceipt(DateOnly DueOn, decimal Amount);

public sealed class RecurringAllowanceHandler(
    ICurrentUser currentUser, IHouseholdRepository households,
    IRecurringAllowanceRepository rules, IPersonalMoneyRepository personalMoney,
    IUnitOfWork uow, TimeProvider clock)
{
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private Guid UserId => currentUser.UserId ?? throw new AuthenticationFailedException();

    private async Task<(Household Household, HouseholdMember Member)> MemberAsync(
        Guid householdId, Guid userId, CancellationToken ct)
    {
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        var member = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
        return (household, member);
    }

    private static RecurringAllowanceView View(RecurringAllowanceRule rule)
        => new(rule.Id, rule.RecipientMemberId, rule.Currency.Value, rule.Amount, rule.StartsOn,
            rule.EndsOn, rule.NextDueOn, rule.IsPaused, rule.IsStopped, rule.IsBlocked, rule.Version);

    public async Task<IReadOnlyList<RecurringAllowanceView>> ListAsync(Guid householdId, CancellationToken ct)
    {
        var (_, actor) = await MemberAsync(householdId, UserId, ct);
        return (await rules.ListAsync(householdId, ct))
            .Where(x => actor.Role == HouseholdRole.Owner || x.RecipientMemberId == actor.Id)
            .Select(View).ToArray();
    }

    public async Task<IReadOnlyList<RecurringAllowanceReceipt>> HistoryAsync(
        Guid householdId, Guid ruleId, CancellationToken ct)
    {
        var (_, actor) = await MemberAsync(householdId, UserId, ct);
        var rule = await rules.GetAsync(householdId, ruleId, ct)
            ?? throw new NotFoundException("allowance schedule", ruleId);
        if (actor.Role != HouseholdRole.Owner && rule.RecipientMemberId != actor.Id)
            throw new NotFoundException("allowance schedule", ruleId);
        return (await rules.HistoryAsync(householdId, ruleId, ct))
            .Select(x => new RecurringAllowanceReceipt(x.OccurredOn, x.Amount)).ToArray();
    }

    public async Task<Guid> CreateAsync(Guid householdId, RecurringAllowanceInput input,
        CancellationToken ct)
    {
        if (input.OperationId == Guid.Empty || !CurrencyCode.IsSupported(input.Currency)
            || input.StartsOn < Today || input.StartsOn == default)
            throw Invalid();
        ValidateAmount(input.Amount);
        if (input.EndsOn < input.StartsOn) throw Invalid();

        await using var tx = await uow.BeginTransactionAsync(ct);
        var (household, actor) = await MemberAsync(householdId, UserId, ct);
        if (actor.Role != HouseholdRole.Owner) throw new ForbiddenException();
        if (!household.Members.Any(x => x.Id == input.RecipientMemberId && x.LeftAtUtc is null))
            throw new NotFoundException("member", input.RecipientMemberId);
        var all = await rules.ListAsync(householdId, ct);
        var existing = all.SingleOrDefault(x => x.CreationOperationId == input.OperationId);
        if (existing is not null)
        {
            if (existing.RecipientMemberId != input.RecipientMemberId
                || existing.Currency.Value != input.Currency || existing.Amount != input.Amount
                || existing.StartsOn != input.StartsOn || existing.EndsOn != input.EndsOn)
                throw Changed();
            await tx.CommitAsync(ct);
            return existing.Id;
        }
        // One currency per membership: never create an incompatible pending rule.
        var wallet = await personalMoney.GetWalletAsync(householdId, input.RecipientMemberId, ct);
        if (wallet is not null && wallet.Currency.Value != input.Currency)
            throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
        if (all.Count(x => !x.IsStopped && x.NextDueOn is not null) >= 100) throw Invalid();
        var rule = RecurringAllowanceRule.Create(householdId, input.RecipientMemberId,
            CurrencyCode.From(input.Currency), input.Amount, input.StartsOn, input.EndsOn,
            input.OperationId, clock.GetUtcNow());
        rules.Add(rule);
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rule.Id;
    }

    public async Task UpdateAsync(Guid householdId, Guid id,
        RecurringAllowanceUpdate input, CancellationToken ct)
    {
        ValidateAmount(input.Amount);
        await using var tx = await uow.BeginTransactionAsync(ct);
        var (_, actor) = await MemberAsync(householdId, UserId, ct);
        if (actor.Role != HouseholdRole.Owner) throw new ForbiddenException();
        var rule = await rules.GetAsync(householdId, id, ct)
            ?? throw new NotFoundException("allowance schedule", id);
        if (rule.Version != input.ExpectedVersion || rule.IsStopped) throw Changed();
        if (input.IsStopped) rule.Stop(clock.GetUtcNow());
        else
        {
            if (input.EndsOn < rule.StartsOn) throw Invalid();
            var wallet = await personalMoney.GetWalletAsync(householdId, rule.RecipientMemberId, ct);
            if (wallet is not null && wallet.Currency != rule.Currency)
                throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
            rule.Update(input.Amount, input.EndsOn, input.IsPaused, clock.GetUtcNow());
        }
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    // This is invoked by the worker, never from an unauthenticated public endpoint.
    // Household row locking serializes schedule edits and occurrence posting across workers.
    public async Task<bool> ProcessAsync(RecurringAllowanceCandidate candidate, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var (household, recipient) = await MemberAsync(candidate.HouseholdId,
            candidate.RecipientUserId, ct);
        if (recipient.Id != candidate.RecipientMemberId
            || !household.Members.Any(x => x.Role == HouseholdRole.Owner && x.LeftAtUtc is null))
            return false;
        var rule = await rules.GetAsync(candidate.HouseholdId, candidate.RuleId, ct);
        if (rule is null || rule.Version != candidate.Version || rule.NextDueOn != candidate.DueOn
            || rule.IsPaused || rule.IsStopped || rule.IsBlocked || candidate.DueOn > Today
            || rule.RecipientMemberId != recipient.Id) return false;
        // The unique (rule, due date) ledger index prevents double granting, including retries.
        var alreadyPosted = await rules.HasReceiptAsync(rule.Id, candidate.DueOn, ct);
        if (!alreadyPosted)
        {
            var wallet = await personalMoney.GetWalletAsync(candidate.HouseholdId, recipient.Id, ct);
            if (wallet is not null && wallet.Currency != rule.Currency)
                throw new BusinessRuleException("personal_money_currency_mismatch", "Error_PersonalMoneyCurrencyMismatch");
            if (wallet is null)
            {
                wallet = PersonalMoneyWallet.Create(candidate.HouseholdId, recipient.Id,
                    rule.Currency, clock.GetUtcNow());
                personalMoney.Add(wallet);
            }
            personalMoney.Add(PersonalMoneyEntry.Create(candidate.HouseholdId, wallet.Id,
                PersonalMoneyEntryKind.Grant, rule.Amount, candidate.DueOn, clock.GetUtcNow(),
                recurringAllowanceRuleId: rule.Id));
        }
        rule.Advance(clock.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task BlockAsync(RecurringAllowanceCandidate candidate, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var (_, recipient) = await MemberAsync(candidate.HouseholdId, candidate.RecipientUserId, ct);
        if (recipient.Id != candidate.RecipientMemberId) return;
        var rule = await rules.GetAsync(candidate.HouseholdId, candidate.RuleId, ct);
        if (rule is null || rule.Version != candidate.Version || rule.IsPaused || rule.IsStopped)
            return;
        rule.Block(clock.GetUtcNow());
        await uow.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static void ValidateAmount(decimal amount)
    {
        if (amount <= 0 || amount > 999999999999999.9999m || decimal.Round(amount, 4) != amount)
            throw Invalid();
    }
    private static BusinessRuleException Invalid() =>
        new("recurring_allowance_invalid", "Error_PersonalMoneyRecurringInvalid");
    private static ConflictException Changed() =>
        new("recurring_allowance_changed", "Error_RecurringChanged");
}
