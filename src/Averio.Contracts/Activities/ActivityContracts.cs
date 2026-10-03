using System.Text.Json.Serialization;
namespace Averio.Contracts.Activities;

public sealed record CreateActivityRequest(string Name, DateOnly? StartsOn, DateOnly? EndsOn);
public sealed record UpdateActivityRequest(string Name, DateOnly? StartsOn, DateOnly? EndsOn,
    bool IsClosed, [property: JsonRequired] DateTimeOffset ExpectedUpdatedAt);
public sealed record CreateActivityResponse(Guid Id);
public sealed record ActivityCurrencyResponse(string Currency, string Income, string Expenses, string Net, long TransactionCount);
public sealed record ActivityDayResponse(DateOnly Date, string Currency, string Income, string Expenses, long TransactionCount);
public sealed record ActivityResponse(Guid Id, string Name, DateOnly? StartsOn, DateOnly? EndsOn,
    bool IsClosed, DateTimeOffset UpdatedAt, IReadOnlyList<ActivityCurrencyResponse> Totals);
public sealed record ActivityAssignmentRequest(Guid? ActivityId, [property: JsonRequired] Guid ExpectedVersion);
public sealed record ActivityAssignmentChangeRequest(Guid TransactionId, [property: JsonRequired] Guid ExpectedVersion);
public sealed record AssignActivitiesRequest(Guid? ActivityId,
    [property: JsonRequired] IReadOnlyList<ActivityAssignmentChangeRequest> Transactions);
