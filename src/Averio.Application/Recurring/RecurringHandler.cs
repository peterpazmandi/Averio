using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using FluentValidation;
namespace Averio.Application.Recurring;

public sealed record RecurringInput(Guid AccountId, Guid CategoryId, string Name, decimal Amount,
    TransactionKind Kind, TransactionVisibility Visibility, RecurringFrequency Frequency,
    DateOnly StartsOn, DateOnly? EndsOn, bool Automatic, bool IsPaused = false, bool IsStopped = false);
public sealed record RecurringResult(RecurringRule Rule, string Currency, bool CanRecord);

public sealed class RecurringHandler(ICurrentUser currentUser, IHouseholdRepository households,
    IAccountRepository accounts, IRecurringRepository rules, IUnitOfWork uow,
    TransactionReferenceResolver references, IValidator<TransactionInput> validator,
    CreateTransactionHandler posting, TimeProvider clock)
{
    private Guid UserId => currentUser.UserId ?? throw new AuthenticationFailedException();
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
    private async Task<HouseholdMember> Member(Guid householdId, Guid userId, CancellationToken ct)
    {
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
    }
    public async Task<IReadOnlyList<RecurringResult>> ListAsync(Guid householdId, CancellationToken ct)
    {
        var member = await Member(householdId, UserId, ct);
        var visible = await accounts.GetVisibleAsync(householdId, member.Id, true, ct);
        return (await rules.ListAsync(householdId, member.Id, ct))
            .Where(r => visible.Any(a => a.Id == r.AccountId))
            .Select(r => { var a = visible.Single(a => a.Id == r.AccountId);
                return new RecurringResult(r, a.Currency.Value, a.Status == AccountStatus.Active && TransactionAccess.CanModify(a, member)
                    && TransactionAccess.IsVisibilityAllowed(a, r.Visibility)); }).ToArray();
    }
    public async Task<Guid> SaveAsync(Guid householdId, Guid? id, Guid? expected, RecurringInput input, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, UserId, ct);
        var rule = id is Guid rid ? await rules.GetAsync(householdId, member.Id, rid, ct)
            ?? throw new NotFoundException("recurring rule", rid) : null;
        if (rule is not null && (expected != rule.Version || rule.IsStopped)) throw Changed();
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 100 || !Enum.IsDefined(input.Frequency)
            || input.StartsOn == default || input.EndsOn < input.StartsOn
            || (rule is null && (input.StartsOn < Today || input.IsStopped || input.IsPaused))) throw Invalid();
        if (rule is not null && (input.AccountId != rule.AccountId || input.Kind != rule.Kind
            || input.Frequency != rule.Frequency || input.StartsOn != rule.StartsOn)) throw Invalid();
        // Stopping/pausing must remain possible when a category or account was archived.
        await ValidateAsync(householdId, member, input, (!input.IsPaused && !input.IsStopped) || (rule is not null && input.CategoryId != rule.CategoryId), ct);
        if (rule is null)
        {
            if ((await rules.ListAsync(householdId, member.Id, ct)).Count(x => !x.IsStopped) >= 100) throw Invalid();
            rule = RecurringRule.Create(householdId, member.Id, input.AccountId, input.CategoryId, input.Name, input.Amount,
                input.Kind, input.Visibility, input.Frequency, input.StartsOn, input.EndsOn, input.Automatic, clock.GetUtcNow());
            rules.Add(rule);
        }
        else rule.Update(input.CategoryId, input.Name, input.Amount, input.Visibility, input.EndsOn,
            input.Automatic, input.IsPaused, input.IsStopped, clock.GetUtcNow());
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rule.Id;
    }
    private async Task ValidateAsync(Guid householdId, HouseholdMember member, RecurringInput input, bool active, CancellationToken ct)
    {
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, input.AccountId, ct)
            ?? throw new NotFoundException("account", input.AccountId);
        if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        if (!Enum.IsDefined(input.Visibility) || !TransactionAccess.IsVisibilityAllowed(account, input.Visibility)) throw Invalid();
        var items = new[] { new TransactionItemInput(input.Name, input.CategoryId, input.Amount) };
        await validator.ValidateAndThrowAsync(new TransactionInput(input.AccountId, input.Kind, input.Visibility,
            null, null, input.StartsOn, items, []), ct);
        if (active)
        {
            if (account.Status != AccountStatus.Active) throw new ConflictException("account_archived", "Error_AccountArchived");
            await references.ResolveItemsAsync(householdId, input.Kind, items, ct);
        }
    }
    public async Task<IReadOnlyList<RecurringOccurrence>> HistoryAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var member = await Member(householdId, UserId, ct);
        var rule = await rules.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("recurring rule", id);
        _ = await accounts.GetVisibleByIdAsync(householdId, member.Id, rule.AccountId, ct) ?? throw new NotFoundException("recurring rule", id);
        return await rules.HistoryAsync(householdId, member.Id, id, ct);
    }
    public async Task<Guid?> ResolveAsync(Guid householdId, Guid id, Guid expected, DateOnly dueOn, bool skip, decimal? amount, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, UserId, ct);
        var rule = await rules.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("recurring rule", id);
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, rule.AccountId, ct) ?? throw new NotFoundException("recurring rule", id);
        if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        var previous = await rules.OccurrenceAsync(id, dueOn, ct);
        if (previous is not null)
        {
            if (skip != (previous.TransactionId is null) || (!skip && amount is decimal actual && actual != previous.Amount)) throw Changed();
            return previous.TransactionId;
        }
        if (rule.Version != expected) throw Changed();
        var result = await ResolveLocked(rule, UserId, dueOn, skip, amount, ct);
        await tx.CommitAsync(ct); return result;
    }
    public async Task<bool> AutomaticAsync(RecurringCandidate candidate, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(candidate.HouseholdId, candidate.UserId, ct);
        var rule = await rules.GetAsync(candidate.HouseholdId, member.Id, candidate.RuleId, ct);
        if (rule is null || rule.Version != candidate.Version || !rule.Automatic || rule.IsPaused || rule.IsStopped
            || rule.IsBlocked || rule.NextDueOn is not DateOnly due || due > Today) return false;
        await ResolveLocked(rule, candidate.UserId, due, false, null, ct);
        await tx.CommitAsync(ct); return true;
    }
    public async Task BlockAsync(RecurringCandidate candidate, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(candidate.HouseholdId, candidate.UserId, ct);
        var rule = await rules.GetAsync(candidate.HouseholdId, member.Id, candidate.RuleId, ct);
        if (rule is null || rule.Version != candidate.Version) return;
        rule.Block(clock.GetUtcNow()); await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<Guid?> ResolveLocked(RecurringRule rule, Guid userId, DateOnly dueOn, bool skip, decimal? amount, CancellationToken ct)
    {
        if (rule.IsPaused || rule.IsStopped || rule.NextDueOn != dueOn || dueOn > Today) throw Changed();
        Guid? transactionId = null;
        if (!skip)
            transactionId = await posting.CreateWithinTransactionAsync(new CreateTransactionCommand(rule.HouseholdId,
                rule.AccountId, rule.Kind, rule.Visibility, null, null, dueOn,
                [new TransactionItemInput(rule.Name, rule.CategoryId, amount ?? rule.Amount)], []), userId, ct);
        rules.Add(RecurringOccurrence.Create(rule, dueOn, transactionId, skip ? null : amount ?? rule.Amount, clock.GetUtcNow()));
        rule.Advance(clock.GetUtcNow()); await uow.SaveChangesAsync(ct);
        return transactionId;
    }
    private static ConflictException Changed() => new("recurring_changed", "Error_RecurringChanged");
    private static BusinessRuleException Invalid() => new("recurring_invalid", "Validation_Recurring");
}
