namespace Averio.Contracts.PersonalMoney;

public sealed record GrantPersonalMoneyRequest(Guid RecipientMemberId, string Currency,
    string Amount, DateOnly OccurredOn);

public sealed record ChargePersonalMoneyRequest(Guid TransactionId, string Amount);

public sealed record PersonalMoneyEventResponse(Guid Id, string Kind, string Amount, DateOnly OccurredOn);

public sealed record PersonalMoneySnapshotResponse(Guid WalletId, string Currency,
    string Available, string AttributedPrivateFunding, string FundingDifference,
    IReadOnlyList<PersonalMoneyEventResponse> Entries);
