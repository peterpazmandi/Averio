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
    IReadOnlyList<ReimbursementEventResponse> Events, int PaymentDelayDays, int? SubmissionReminderDays, IReadOnlyList<ReimbursementPaymentResponse> Payments);
public sealed record ReimbursementEventResponse(string Status, DateOnly? SubmittedOn, string? Reference, DateTimeOffset RecordedAt,
    string? ChangeKind, string? Provider, string? PotentialAmount, Guid? DestinationAccountId, int? PaymentDelayDays, DateOnly? SubmissionReminderOn, DateOnly? ExpectedOn);
public sealed record UpdateReimbursementClaimRequest(Guid ExpectedVersion, RecurringReimbursementConfiguration Configuration);

public sealed record CreateReimbursementClaimRequest(Guid TransactionId, DateTimeOffset ExpectedUpdatedAt, RecurringReimbursementConfiguration Configuration);
public sealed record CreateReimbursementClaimResponse(Guid Id);

public sealed record LinkReimbursementPaymentRequest(Guid CreditTransactionId, Guid OperationId, Guid ExpectedVersion, DateTimeOffset ExpectedCreditUpdatedAt, bool MoneyReceived, string? Amount = null);
public sealed record LinkReimbursementPaymentResponse(Guid Id);
public sealed record UnlinkReimbursementPaymentRequest(Guid ExpectedVersion);
public sealed record ReimbursementPaymentResponse(Guid Id, Guid CreditTransactionId, string Amount, DateOnly ReceivedOn, DateTimeOffset LinkedAt, DateTimeOffset? UnlinkedAt);
public sealed record ReimbursementPaymentCandidateResponse(Guid Id, string Description, string Amount, DateOnly ReceivedOn, DateTimeOffset UpdatedAt, string AvailableAmount);
public sealed record ReimbursementPaymentCandidatesResponse(IReadOnlyList<ReimbursementPaymentCandidateResponse> Items, bool HasMore);

// Opaque references only: never expose inaccessible financial details through recovery.
public sealed record ReimbursementRecoveryLinkResponse(Guid ClaimId, Guid PaymentId);
public sealed record RecoverReimbursementPaymentsRequest(IReadOnlyList<ReimbursementRecoveryLinkResponse> Links);
public sealed record MoneyBackItemResponse(string Kind, Guid Id, string Description, string? Provider, string? Merchant, string Currency, string Amount, string? OutstandingAmount, DateOnly Date, string Status, Guid? SourceTransactionId, Guid? CreditTransactionId, bool CanModify);
public sealed record MoneyBackPageResponse(IReadOnlyList<MoneyBackItemResponse> Items, bool HasMore, int Offset, int Limit);
