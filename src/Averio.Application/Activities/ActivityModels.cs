using Averio.Domain.Activities;
using FluentValidation;

namespace Averio.Application.Activities;

public sealed record ActivityInput(string Name, DateOnly? StartsOn, DateOnly? EndsOn, bool IsClosed = false);
public sealed class ActivityInputValidator : AbstractValidator<ActivityInput>
{
    public ActivityInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithErrorCode("Validation_ActivityName")
            .MaximumLength(100).WithErrorCode("Validation_ActivityName");
        RuleFor(x => x.EndsOn).Must((x, end) => x.StartsOn is null || end is null || end >= x.StartsOn)
            .WithErrorCode("Validation_ActivityDates");
    }
}
public sealed record ActivityAssignmentInput(Guid? ActivityId, Guid ExpectedVersion);
public sealed record ActivityAssignmentChange(Guid TransactionId, Guid ExpectedVersion);
public sealed record ActivityCurrencyResult(string Currency, decimal Income, decimal Expenses, long TransactionCount)
{
    public decimal Net => Income - Expenses;
}
public sealed record ActivityResult(Activity Activity, IReadOnlyList<ActivityCurrencyResult> Totals);
public sealed record ActivityDayResult(DateOnly Date, string Currency, decimal Income, decimal Expenses, long TransactionCount);
public sealed record ActivityAssignmentResult(Guid? ActivityId, string? ActivityName, Guid Version);

public interface IActivityRepository
{
    Task LockMemberAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<Activity?> GetAsync(Guid householdId, Guid memberId, Guid id, CancellationToken ct);
    Task<IReadOnlyList<ActivityResult>> ListAsync(Guid householdId, Guid memberId, DateOnly through, CancellationToken ct);
    Task<IReadOnlyList<ActivityDayResult>> TimelineAsync(Guid householdId, Guid memberId, Guid activityId, DateOnly through, CancellationToken ct);
    Task<ActivityAssignment?> GetAssignmentAsync(Guid householdId, Guid memberId, Guid transactionId, CancellationToken ct);
    Task<ActivityAssignmentResult?> ReadAssignmentAsync(Guid householdId, Guid memberId, Guid transactionId, CancellationToken ct);
    void Add(Activity activity);
    void Add(ActivityAssignment assignment);
}
