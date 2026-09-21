using System.Text.Json.Serialization;

namespace Averio.Contracts.Transactions;

public sealed record CreateTransactionRequest(
    Guid AccountId,
    string Kind,
    string Visibility,
    [property: JsonRequired] string Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn);

public sealed record UpdateTransactionRequest(
    Guid AccountId,
    string Kind,
    string Visibility,
    [property: JsonRequired] string Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn,
    [property: JsonRequired] DateTimeOffset ExpectedUpdatedAt);

public sealed record CreateTransactionResponse(Guid Id);

public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Currency,
    string Kind,
    string Visibility,
    string Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanModify);

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    bool HasMore);
