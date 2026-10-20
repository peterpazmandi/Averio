namespace Averio.Contracts.PersonalMoney;

public sealed record CreateRecurringAllowanceRequest(Guid RecipientMemberId, string Currency,
    string Amount, DateOnly StartsOn, DateOnly? EndsOn, Guid OperationId);
public sealed record UpdateRecurringAllowanceRequest(string Amount, DateOnly? EndsOn,
    bool IsPaused, bool IsStopped, Guid ExpectedVersion);
public sealed record RecurringAllowanceResponse(Guid Id, Guid RecipientMemberId, string Currency,
    string Amount, DateOnly StartsOn, DateOnly? EndsOn, DateOnly? NextDueOn,
    bool IsPaused, bool IsStopped, bool IsBlocked, Guid Version);
public sealed record RecurringAllowanceHistoryResponse(DateOnly DueOn, string Amount);
