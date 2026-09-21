using Averio.Domain.Labels;
using Averio.Domain.Transactions;

namespace Averio.Application.Common.Abstractions;

public interface ITransactionRepository
{
    void Add(Transaction transaction);

    Task<Transaction?> GetVisibleByIdAsync(
        Guid householdId,
        Guid householdMemberId,
        Guid transactionId,
        CancellationToken cancellationToken);

    Task<TransactionReadPage> GetVisiblePageAsync(
        Guid householdId,
        Guid householdMemberId,
        TransactionQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<TransactionReadModel>> GetLatestVisibleAsync(
        Guid householdId,
        Guid householdMemberId,
        int take,
        CancellationToken cancellationToken);
}

public sealed record TransactionQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? AccountId,
    TransactionKind? Kind,
    string? Search,
    int PageSize,
    DateOnly? CursorDate,
    DateTimeOffset? CursorCreatedAt);

public sealed record TransactionReadPage(
    IReadOnlyList<TransactionReadModel> Items,
    bool HasMore);

public sealed record TransactionItemReadModel(
    Guid Id,
    int Position,
    string Description,
    Guid CategoryId,
    string CategoryName,
    string CategoryIcon,
    decimal Amount);

public sealed record TransactionLabelReadModel(
    Guid Id,
    string Name,
    LabelScope Scope);

public sealed record TransactionReadModel(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Currency,
    TransactionKind Kind,
    TransactionVisibility Visibility,
    decimal Amount,
    string Description,
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    TransactionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanModify,
    IReadOnlyList<TransactionItemReadModel> Items,
    IReadOnlyList<TransactionLabelReadModel> Labels);
