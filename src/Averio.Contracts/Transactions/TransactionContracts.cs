using System.Text.Json.Serialization;

namespace Averio.Contracts.Transactions;

public sealed record TransactionItemRequest(
    string Description,
    string? CategoryName,
    [property: JsonRequired] string Amount);

public sealed record TransactionLabelRequest(
    string Name,
    string Scope);

public sealed record CreateTransactionRequest(
    Guid AccountId,
    string Kind,
    string Visibility,
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    [property: JsonRequired] IReadOnlyList<TransactionItemRequest> Items,
    IReadOnlyList<TransactionLabelRequest>? Labels);

public sealed record UpdateTransactionRequest(
    Guid AccountId,
    string Kind,
    string Visibility,
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    [property: JsonRequired] IReadOnlyList<TransactionItemRequest> Items,
    IReadOnlyList<TransactionLabelRequest>? Labels,
    [property: JsonRequired] DateTimeOffset ExpectedUpdatedAt);

public sealed record CreateTransactionResponse(Guid Id);

public sealed record TransactionItemResponse(
    Guid Id,
    int Position,
    string Description,
    string? CategoryName,
    string Amount);

public sealed record TransactionLabelResponse(
    Guid Id,
    string Name,
    string Scope);

public sealed record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Currency,
    string Kind,
    string Visibility,
    string Amount,
    string Description,
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanModify,
    IReadOnlyList<TransactionItemResponse> Items,
    IReadOnlyList<TransactionLabelResponse> Labels);

public sealed record TransactionPageResponse(
    IReadOnlyList<TransactionResponse> Items,
    bool HasMore);
