using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Labels;
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
        Guid? cursorId,
        CancellationToken cancellationToken,
        Guid? activityId = null, IReadOnlyList<Guid>? labelIds = null,
        bool includeArchived = false, bool importedOnly = false)
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
        if (labelIds?.Count > 20) throw new BusinessRuleException("saved_view_invalid", "Validation_SavedView");

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
                cursorCreatedAt,
                cursorId, activityId, labelIds?.Distinct().ToArray(), includeArchived, importedOnly),
            cancellationToken);

        return new TransactionPageResult(
            page.Items.Select(TransactionResult.From).ToArray(),
            page.HasMore);
    }
}

public sealed record TransactionPageResult(
    IReadOnlyList<TransactionResult> Items,
    bool HasMore);

public sealed record TransactionItemResult(
    Guid Id,
    int Position,
    string Description,
    Guid CategoryId,
    string CategoryName,
    string CategoryIcon,
    decimal Amount);

public sealed record TransactionLabelResult(
    Guid Id,
    string Name,
    LabelScope Scope);

public sealed record TransactionResult(
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
    IReadOnlyList<TransactionLabelResult> Labels,
    Guid? ActivityId = null, string? ActivityName = null, Guid ActivityAssignmentVersion = default,
    bool IsArchived = false, bool IsImported = false, Guid? ImportBatchId = null)
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
        model.Merchant,
        model.Note,
        model.OccurredOn,
        model.Status,
        model.CreatedAt,
        model.UpdatedAt,
        model.CanModify,
        model.Items.Select(item => new TransactionItemResult(
            item.Id,
            item.Position,
            item.Description,
            item.CategoryId,
            item.CategoryName,
            item.CategoryIcon,
            item.Amount)).ToArray(),
        model.Labels.Select(label => new TransactionLabelResult(
            label.Id,
            label.Name,
            label.Scope)).ToArray(),
        model.ActivityId, model.ActivityName, model.ActivityAssignmentVersion,
        model.IsArchived, model.IsImported, model.ImportBatchId);
}
