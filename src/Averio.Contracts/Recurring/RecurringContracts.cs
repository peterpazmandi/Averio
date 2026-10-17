namespace Averio.Contracts.Recurring;

public sealed record SaveRecurringRuleRequest(Guid AccountId, Guid CategoryId, string Name, string Amount,
    string Kind, string Visibility, string Frequency, DateOnly StartsOn, DateOnly? EndsOn,
    bool Automatic = false, bool IsPaused = false, bool IsStopped = false, Guid? ExpectedVersion = null,
    IReadOnlyList<RecurringItemRequest>? Items = null, IReadOnlyList<Guid>? LabelIds = null,
    string? Merchant = null, string? Note = null, Guid? ActivityId = null);
public sealed record RecurringRuleResponse(Guid Id, Guid AccountId, Guid CategoryId, string Name, string Amount,
    string Currency, string Kind, string Visibility, string Frequency, DateOnly StartsOn, DateOnly? EndsOn,
    DateOnly? NextDueOn, bool Automatic, bool IsPaused, bool IsStopped, bool IsBlocked, bool CanRecord, Guid Version,
    IReadOnlyList<RecurringItemRequest> Items, IReadOnlyList<Guid> LabelIds,
    string? Merchant, string? Note, Guid? ActivityId);
public sealed record RecurringOccurrenceRequest(Guid ExpectedVersion, DateOnly DueOn, bool Skip = false, string? Amount = null, IReadOnlyList<string>? ItemAmounts = null);
public sealed record RecurringOccurrenceResponse(DateOnly DueOn, Guid? TransactionId, DateTimeOffset ProcessedAt);
public sealed record RecurringResolutionResponse(Guid? TransactionId);
public sealed record CreateRecurringRuleResponse(Guid Id);

public sealed record RecurringItemRequest(string Description, Guid CategoryId, string Amount);
