namespace Averio.Contracts.Recurring;

public sealed record SaveRecurringRuleRequest(Guid AccountId, Guid CategoryId, string Name, string Amount,
    string Kind, string Visibility, string Frequency, DateOnly StartsOn, DateOnly? EndsOn,
    bool Automatic = false, bool IsPaused = false, bool IsStopped = false, Guid? ExpectedVersion = null,
    IReadOnlyList<RecurringItemRequest>? Items = null, IReadOnlyList<Guid>? LabelIds = null,
    string? Merchant = null, string? Note = null, Guid? ActivityId = null, RecurringReimbursementConfiguration? Reimbursement = null);
public sealed record RecurringRuleResponse(Guid Id, Guid AccountId, Guid CategoryId, string Name, string Amount,
    string Currency, string Kind, string Visibility, string Frequency, DateOnly StartsOn, DateOnly? EndsOn,
    DateOnly? NextDueOn, bool Automatic, bool IsPaused, bool IsStopped, bool IsBlocked, bool CanRecord, Guid Version,
    IReadOnlyList<RecurringItemRequest> Items, IReadOnlyList<Guid> LabelIds,
    string? Merchant, string? Note, Guid? ActivityId, RecurringReimbursementConfiguration? Reimbursement);
public sealed record RecurringOccurrenceRequest(Guid ExpectedVersion, DateOnly DueOn, bool Skip = false, string? Amount = null, IReadOnlyList<string>? ItemAmounts = null);
public sealed record RecurringOccurrenceResponse(DateOnly DueOn, Guid? TransactionId, DateTimeOffset ProcessedAt);
public sealed record RecurringResolutionResponse(Guid? TransactionId);
public sealed record CreateRecurringRuleResponse(Guid Id);

public sealed record RecurringItemRequest(string Description, Guid CategoryId, string Amount);

public sealed record RecurringReimbursementConfiguration(string Provider, string Calculation, string Value,
    string? MaximumAmount, Guid DestinationAccountId, int PaymentDelayDays, int? SubmissionReminderDays);
public sealed record ChangeReimbursementRequest(Guid ExpectedVersion, string Status, bool ReceiptSubmitted = false,
    DateOnly? SubmittedOn = null, string? Reference = null);
public sealed record ReimbursementClaimResponse(Guid Id, Guid TransactionId, string Description, string Provider,
    string Currency, string PotentialAmount, string ExpectedAmount, string ReceivedAmount, string OutstandingAmount,
    string OriginalPaymentAmount, DateOnly PaymentOn, Guid DestinationAccountId, string Status,
    DateOnly? SubmissionReminderOn, DateOnly? SubmittedOn, DateOnly? ExpectedOn, string? Reference, Guid Version,
    bool CanModify, bool PaymentUnavailable, bool PaymentChanged, bool SubmissionReminderDue, bool PaymentFollowUpDue,
    IReadOnlyList<ReimbursementEventResponse> Events);
public sealed record ReimbursementEventResponse(string Status, DateOnly? SubmittedOn, string? Reference, DateTimeOffset RecordedAt);

public sealed record CreateReimbursementClaimRequest(Guid TransactionId, DateTimeOffset ExpectedUpdatedAt, RecurringReimbursementConfiguration Configuration);
public sealed record CreateReimbursementClaimResponse(Guid Id);
