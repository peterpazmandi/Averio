using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.GetTransactions;
using Averio.Domain.Labels;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.GetTransaction;

public sealed class GetTransactionHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ILabelRepository labelRepository,
    ITransactionRepository transactionRepository)
{
    public async Task<TransactionDetailResult> HandleAsync(
        Guid householdId,
        Guid transactionId,
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
        var transaction = await transactionRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            transactionId,
            cancellationToken)
            ?? throw new NotFoundException("transaction", transactionId);

        var account = await accountRepository.GetVisibleByIdAsync(
            household.Id,
            membership.Id,
            transaction.AccountId,
            cancellationToken)
            ?? throw new NotFoundException("account", transaction.AccountId);

        var labelIds = transaction.Labels.Select(link => link.LabelId).ToArray();
        IReadOnlyList<Label> labels = labelIds.Length == 0
            ? []
            : await labelRepository.GetApplicableByIdsAsync(
                household.Id,
                account.Id,
                labelIds,
                cancellationToken);

        return new TransactionDetailResult(
            transaction.Id,
            transaction.AccountId,
            account.Name,
            transaction.Currency.Value,
            transaction.Kind,
            transaction.Visibility,
            transaction.Amount,
            transaction.Description,
            transaction.Merchant,
            transaction.Note,
            transaction.OccurredOn,
            transaction.Status,
            transaction.CreatedAt,
            transaction.UpdatedAt,
            TransactionAccess.CanModify(account, membership),
            transaction.Items
                .OrderBy(item => item.Position)
                .Select(item => new TransactionItemResult(
                    item.Id,
                    item.Position,
                    item.Description,
                    item.CategoryId,
                    item.CategoryName,
                    item.CategoryIcon,
                    item.Amount))
                .ToArray(),
            labels
                .OrderBy(label => label.Scope)
                .ThenBy(label => label.Name)
                .Select(label => new TransactionLabelResult(
                    label.Id,
                    label.Name,
                    label.Scope))
                .ToArray());
    }
}

public sealed record TransactionDetailResult(
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
    IReadOnlyList<TransactionItemResult> Items,
    IReadOnlyList<TransactionLabelResult> Labels);
