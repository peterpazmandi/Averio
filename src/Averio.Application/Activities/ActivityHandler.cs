using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Activities;
using Averio.Domain.Transactions;
using FluentValidation;

namespace Averio.Application.Activities;

public sealed class ActivityHandler(ICurrentUser currentUser, IHouseholdRepository households,
    IActivityRepository activities, ITransactionRepository transactions,
    IValidator<ActivityInput> validator, IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<IReadOnlyList<ActivityResult>> ListAsync(Guid householdId, CancellationToken ct)
    {
        var memberId = await AuthorizeAsync(householdId, ct);
        return await activities.ListAsync(householdId, memberId, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct);
    }
    public async Task<Guid> CreateAsync(Guid householdId, ActivityInput input, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var memberId = await AuthorizeAsync(householdId, ct);
        await validator.ValidateAndThrowAsync(input, ct);
        var activity = Activity.Create(householdId, memberId, input.Name, input.StartsOn, input.EndsOn, clock.GetUtcNow());
        activities.Add(activity);
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return activity.Id;
    }
    public async Task<IReadOnlyList<ActivityDayResult>> TimelineAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var memberId = await AuthorizeAsync(householdId, ct);
        _ = await activities.GetAsync(householdId, memberId, id, ct)
            ?? throw new NotFoundException("activity", id);
        return await activities.TimelineAsync(householdId, memberId, id,
            DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), ct);
    }
    public async Task UpdateAsync(Guid householdId, Guid id, ActivityInput input, DateTimeOffset expected, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var memberId = await AuthorizeAsync(householdId, ct);
        await validator.ValidateAndThrowAsync(input, ct);
        await activities.LockMemberAsync(householdId, memberId, ct);
        var activity = await activities.GetAsync(householdId, memberId, id, ct)
            ?? throw new NotFoundException("activity", id);
        if (activity.UpdatedAt != expected) throw Changed();
        activity.Update(input.Name, input.StartsOn, input.EndsOn, input.IsClosed, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task AssignAsync(Guid householdId, Guid? activityId, IReadOnlyList<ActivityAssignmentChange> changes, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var memberId = await AuthorizeAsync(householdId, ct);
        if (changes.Count is < 1 or > 100 || changes.Select(x => x.TransactionId).Distinct().Count() != changes.Count)
            throw new BusinessRuleException("activity_selection_invalid", "Validation_ActivitySelection");
        await activities.LockMemberAsync(householdId, memberId, ct);
        foreach (var change in changes)
        {
            var transaction = await transactions.GetVisibleByIdAsync(householdId, memberId, change.TransactionId, ct)
                ?? throw new NotFoundException("transaction", change.TransactionId);
            if (transaction.Status != TransactionStatus.Posted)
                throw new ConflictException("transaction_voided", "Error_TransactionVoided");
            await AssignWithinTransactionAsync(householdId, memberId, transaction.Id,
                new ActivityAssignmentInput(activityId, change.ExpectedVersion), ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    // The caller authorizes the ledger entry and owns the surrounding transaction.
    public async Task AssignWithinTransactionAsync(Guid householdId, Guid memberId, Guid transactionId,
        ActivityAssignmentInput? input, CancellationToken ct)
    {
        if (input is null) return;
        await activities.LockMemberAsync(householdId, memberId, ct);
        var link = await activities.GetAssignmentAsync(householdId, memberId, transactionId, ct);
        if ((link?.Version ?? Guid.Empty) != input.ExpectedVersion) throw Changed();
        Activity? activity = null;
        if (input.ActivityId is Guid id)
            activity = await activities.GetAsync(householdId, memberId, id, ct)
                ?? throw new NotFoundException("activity", id);
        if (link?.ActivityId == input.ActivityId) return;
        if (activity?.IsClosed == true) throw new ConflictException("activity_closed", "Error_ActivityClosed");
        if (link is null)
        {
            link = ActivityAssignment.Create(householdId, memberId, transactionId);
            activities.Add(link);
        }
        link.Assign(activity, clock.GetUtcNow());
    }
    private async Task<Guid> AuthorizeAsync(Guid householdId, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(x => x.UserId == userId).Id;
    }
    private static ConflictException Changed() => new("activity_changed", "Error_ActivityChanged");
}

