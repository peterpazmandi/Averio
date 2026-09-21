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
    Guid? CursorId);

public sealed record TransactionReadPage(
    IReadOnlyList<TransactionReadModel> Items,
    bool HasMore);

public sealed record TransactionReadModel(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string Currency,
    TransactionKind Kind,
    TransactionVisibility Visibility,
    decimal Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn,
    TransactionStatus Status,
    DateTimeOffset UpdatedAt,
    bool CanModify);
