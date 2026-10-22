namespace Averio.Contracts.PersonalMoney;

public sealed record GrantPersonalMoneyRequest(Guid RecipientMemberId, string Currency,
    string Amount, DateOnly OccurredOn, Guid OperationId);

public sealed record ChargePersonalMoneyRequest(Guid TransactionId, string Amount, Guid OperationId);

public sealed record PersonalMoneyEventResponse(Guid Id, string Kind, string Amount, DateOnly OccurredOn,
    Guid? TransactionId, Guid? TransferId, Guid? RelatedTransactionId,
    Guid? RecurringAllowanceRuleId);

public sealed record PersonalMoneySummaryResponse(Guid WalletId, string Currency,
    string Available, string AttributedPrivateFunding, string FundingDifference);

public sealed record PersonalMoneySnapshotResponse(Guid WalletId, string Currency,
    string Available, string AttributedPrivateFunding, string FundingDifference,
    IReadOnlyList<PersonalMoneyEventResponse> Entries, IReadOnlyList<PersonalMoneyChargeResponse> Charges);

public sealed record PersonalMoneyChargeResponse(Guid TransactionId, string Amount, int Version);
public sealed record CorrectPersonalMoneyChargeRequest(string Amount, string ExpectedAmount,
    int ExpectedVersion, Guid OperationId);

public sealed record RefundPersonalMoneyRequest(Guid RefundTransactionId,
    Guid PurchaseTransactionId, string Amount, Guid OperationId);

public sealed record SurrenderPersonalMoneyRequest(string Amount, DateOnly OccurredOn, Guid OperationId);
