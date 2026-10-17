namespace Averio.Contracts.Transfers;

public sealed record CreateTransferRequest(Guid SourceAccountId, Guid DestinationAccountId,
    string SentAmount, string ReceivedAmount, DateOnly OccurredOn, string? Note,
    string? PersonalMoneyPurpose = null);
public sealed record UpdateTransferRequest(string SentAmount, string ReceivedAmount,
    DateOnly OccurredOn, string? Note, DateTimeOffset ExpectedUpdatedAt);
public sealed record CreateTransferResponse(Guid Id);
public sealed record TransferSideResponse(Guid AccountId, string AccountName, string Currency, string Amount);
public sealed record TransferResponse(Guid Id, TransferSideResponse? Source, TransferSideResponse? Destination,
    DateOnly OccurredOn, string? Note, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool IsVoided, bool CanModify);
public sealed record TransferPageResponse(IReadOnlyList<TransferResponse> Items, bool HasMore);
