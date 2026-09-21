using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.GetTransactions;

public sealed class GetTransactionsHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    ITransactionRepository transactionRepository)
{
    public async Task<TransactionPageResult> HandleAsync(
        Guid householdId,
        DateOnly? from,
        DateOnly? to,
        Guid? accountId,
        TransactionKind? kind,
        string? search,
        int pageSize,
        DateOnly? cursorDate,
        DateTimeOffset? cursorCreatedAt,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            householdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var membership = household.Members.Single(member => member.UserId == userId);
        var size = Math.Clamp(pageSize, 1, 100);

        var page = await transactionRepository.GetVisiblePageAsync(
            household.Id,
            membership.Id,
            new TransactionQuery(
                from,
                to,
                accountId,
                kind,
                string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                size,
                cursorDate,
                cursorCreatedAt),
            cancellationToken);

        return new TransactionPageResult(
            page.Items.Select(TransactionResult.From).ToArray(),
            page.HasMore);
    }
}

public sealed record TransactionPageResult(
    IReadOnlyList<TransactionResult> Items,
    bool HasMore);

public sealed record TransactionResult(
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
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanModify)
{
    public static TransactionResult From(TransactionReadModel model) => new(
        model.Id,
        model.AccountId,
        model.AccountName,
        model.Currency,
        model.Kind,
        model.Visibility,
        model.Amount,
        model.Description,
        model.Note,
        model.OccurredOn,
        model.Status,
        model.CreatedAt,
        model.UpdatedAt,
        model.CanModify);
}
