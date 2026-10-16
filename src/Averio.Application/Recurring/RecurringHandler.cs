using Averio.Application.Activities;
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
    DateOnly StartsOn, DateOnly? EndsOn, bool Automatic, bool IsPaused = false, bool IsStopped = false,
    IReadOnlyList<TransactionItemInput>? Items = null, IReadOnlyList<Guid>? LabelIds = null,
    string? Merchant = null, string? Note = null, Guid? ActivityId = null);
public sealed record RecurringResult(RecurringRule Rule, string Currency, bool CanRecord);

public sealed class RecurringHandler(ICurrentUser currentUser, IHouseholdRepository households,
    IAccountRepository accounts, IRecurringRepository rules, IUnitOfWork uow,
    TransactionReferenceResolver references, IValidator<TransactionInput> validator,
    CreateTransactionHandler posting, TimeProvider clock, IActivityRepository activities)
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
        // Owners can suspend their own automation without permission to post to its account.
        var active = !input.IsPaused && !input.IsStopped;
        var items = input.Items ?? [new TransactionItemInput(input.Name, input.CategoryId, input.Amount)];
        var labels = input.LabelIds ?? [];
        if (rule is not null && rule.Items.Count > 1 && input.Items is null) throw Invalid();
        if (input.Items is not null && (items.Count == 0 || input.Amount != items.Sum(i => i.Amount)
            || input.CategoryId != items[0].CategoryId)) throw Invalid();
        await ValidateAsync(householdId, member, input, items, labels, rule, active, ct);
        var drafts = items.Select(i => new RecurringItemDraft(i.Description, i.CategoryId, i.Amount)).ToArray();
        if (rule is null)
        {
            if ((await rules.ListAsync(householdId, member.Id, ct)).Count(x => !x.IsStopped) >= 100) throw Invalid();
            rule = RecurringRule.Create(householdId, member.Id, input.AccountId, input.CategoryId, input.Name, input.Amount,
                input.Kind, input.Visibility, input.Frequency, input.StartsOn, input.EndsOn, input.Automatic, clock.GetUtcNow());
            rule.SetTemplate(drafts, labels, input.Merchant, input.Note, input.ActivityId);
            rules.Add(rule);
        }
        else rule.Update(input.CategoryId, input.Name, input.Amount, input.Visibility, input.EndsOn,
            input.Automatic, input.IsPaused, input.IsStopped, clock.GetUtcNow(), drafts, labels, input.Merchant, input.Note, input.ActivityId);
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct); return rule.Id;
    }
    private async Task ValidateAsync(Guid householdId, HouseholdMember member, RecurringInput input, IReadOnlyList<TransactionItemInput> items, IReadOnlyList<Guid> labels, RecurringRule? existing, bool active, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(new TransactionInput(input.AccountId, input.Kind, input.Visibility,
            input.Merchant, input.Note, input.StartsOn, items, labels), ct);
        if (active)
        {
            var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, input.AccountId, ct)
                ?? throw new NotFoundException("account", input.AccountId);
            if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
            if (!Enum.IsDefined(input.Visibility) || !TransactionAccess.IsVisibilityAllowed(account, input.Visibility)) throw Invalid();
            if (account.Status != AccountStatus.Active) throw new ConflictException("account_archived", "Error_AccountArchived");
        }
        if (active || existing is null || !items.Select(i => i.CategoryId).ToHashSet().SetEquals(existing.Items.Select(i => i.CategoryId)))
            await references.ResolveItemsAsync(householdId, input.Kind, items, ct);
        if (active || existing is null || !labels.ToHashSet().SetEquals(existing.Labels.Select(l => l.LabelId)))
            await references.ResolveLabelIdsAsync(householdId, input.AccountId, labels, ct);
        if (input.ActivityId is Guid activityId && (active || input.ActivityId != existing?.ActivityId))
        {
            var activity = await activities.GetAsync(householdId, member.Id, activityId, ct)
                ?? throw new NotFoundException("activity", activityId);
            if (activity.IsClosed) throw new ConflictException("activity_closed", "Error_ActivityClosed");
        }
    }
    public async Task<IReadOnlyList<RecurringOccurrence>> HistoryAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var member = await Member(householdId, UserId, ct);
        var rule = await rules.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("recurring rule", id);
        _ = await accounts.GetVisibleByIdAsync(householdId, member.Id, rule.AccountId, ct) ?? throw new NotFoundException("recurring rule", id);
        return await rules.HistoryAsync(householdId, member.Id, id, ct);
    }
    public async Task<Guid?> ResolveAsync(Guid householdId, Guid id, Guid expected, DateOnly dueOn, bool skip, decimal? amount, CancellationToken ct, IReadOnlyList<decimal>? itemAmounts = null)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Member(householdId, UserId, ct);
        var rule = await rules.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("recurring rule", id);
        var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, rule.AccountId, ct) ?? throw new NotFoundException("recurring rule", id);
        if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
        if (itemAmounts is not null)
        {
            if (skip || itemAmounts.Count is < 1 or > 100 || itemAmounts.Any(x => x <= 0 || x > 999999999999999.9999m || decimal.Round(x, 4) != x))
                throw Invalid();
            var total = itemAmounts.Sum();
            if (amount is decimal specified && specified != total) throw Invalid();
            amount = total;
        }
        var previous = await rules.OccurrenceAsync(id, dueOn, ct);
        if (previous is not null)
        {
            if (skip != (previous.TransactionId is null) || (!skip && amount is decimal actual && actual != previous.Amount)) throw Changed();
            return previous.TransactionId;
        }
        if (rule.Version != expected) throw Changed();
        if (itemAmounts is not null && itemAmounts.Count != rule.Items.Count) throw Invalid();
        var result = await ResolveLocked(rule, UserId, dueOn, skip, amount, ct, itemAmounts);
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
    private async Task<Guid?> ResolveLocked(RecurringRule rule, Guid userId, DateOnly dueOn, bool skip, decimal? amount, CancellationToken ct, IReadOnlyList<decimal>? itemAmounts = null)
    {
        if (rule.IsPaused || rule.IsStopped || rule.NextDueOn != dueOn || dueOn > Today) throw Changed();
        Guid? transactionId = null;
        var items = rule.Items.OrderBy(i => i.Position).Select((i, index) =>
            new TransactionItemInput(i.Description, i.CategoryId, itemAmounts?[index] ?? (rule.Items.Count == 1 ? amount ?? i.Amount : i.Amount))).ToArray();
        if (!skip && itemAmounts is null && rule.Items.Count > 1 && amount is decimal actual && actual != rule.Amount) throw Invalid();
        if (!skip)
            transactionId = await posting.CreateWithinTransactionAsync(new CreateTransactionCommand(rule.HouseholdId,
                rule.AccountId, rule.Kind, rule.Visibility, rule.Merchant, rule.Note, dueOn,
                items, rule.Labels.Select(l => l.LabelId).ToArray(),
                rule.ActivityId is Guid activityId ? new Averio.Application.Activities.ActivityAssignmentInput(activityId, Guid.Empty) : null), userId, ct);
        rules.Add(RecurringOccurrence.Create(rule, dueOn, transactionId, skip ? null : amount ?? rule.Amount, clock.GetUtcNow()));
        rule.Advance(clock.GetUtcNow()); await uow.SaveChangesAsync(ct);
        return transactionId;
    }
    private static ConflictException Changed() => new("recurring_changed", "Error_RecurringChanged");
    private static BusinessRuleException Invalid() => new("recurring_invalid", "Validation_Recurring");
}
