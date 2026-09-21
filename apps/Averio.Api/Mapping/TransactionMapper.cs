using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.GetTransaction;
using Averio.Application.Transactions.GetTransactions;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Contracts.Transactions;
using Averio.Domain.Transactions;

namespace Averio.Api.Mapping;

internal static class TransactionMapper
{
    public static CreateTransactionCommand ToCommand(
        Guid householdId,
        CreateTransactionRequest request) => new(
            householdId,
            request.AccountId,
            EnumParser.Parse<TransactionKind>(
                request.Kind,
                nameof(request.Kind),
                "Validation_TransactionKindInvalid"),
            EnumParser.Parse<TransactionVisibility>(
                request.Visibility,
                nameof(request.Visibility),
                "Validation_TransactionVisibilityInvalid"),
            request.Merchant,
            request.Note,
            request.OccurredOn,
            MapItems(request.Items),
            MapLabels(request.Labels));

    public static UpdateTransactionCommand ToCommand(
        Guid householdId,
        Guid transactionId,
        UpdateTransactionRequest request) => new(
            householdId,
            transactionId,
            request.AccountId,
            EnumParser.Parse<TransactionKind>(
                request.Kind,
                nameof(request.Kind),
                "Validation_TransactionKindInvalid"),
            EnumParser.Parse<TransactionVisibility>(
                request.Visibility,
                nameof(request.Visibility),
                "Validation_TransactionVisibilityInvalid"),
            request.Merchant,
            request.Note,
            request.OccurredOn,
            MapItems(request.Items),
            MapLabels(request.Labels),
            request.ExpectedUpdatedAt);

    public static TransactionResponse From(TransactionResult result) => new(
        result.Id,
        result.AccountId,
        result.AccountName,
        result.Currency,
        result.Kind.ToString(),
        result.Visibility.ToString(),
        DecimalAmountMapper.Format(result.Amount),
        result.Description,
        result.Merchant,
        result.Note,
        result.OccurredOn,
        result.Status.ToString(),
        result.CreatedAt,
        result.UpdatedAt,
        result.CanModify,
        result.Items.Select(item => new TransactionItemResponse(
            item.Id,
            item.Position,
            item.Description,
            item.CategoryName,
            DecimalAmountMapper.Format(item.Amount))).ToArray(),
        result.Labels.Select(label => new TransactionLabelResponse(
            label.Id,
            label.Name,
            label.Scope.ToString())).ToArray());

    public static TransactionResponse From(TransactionDetailResult result) => new(
        result.Id,
        result.AccountId,
        result.AccountName,
        result.Currency,
        result.Kind.ToString(),
        result.Visibility.ToString(),
        DecimalAmountMapper.Format(result.Amount),
        result.Description,
        result.Merchant,
        result.Note,
        result.OccurredOn,
        result.Status.ToString(),
        result.CreatedAt,
        result.UpdatedAt,
        result.CanModify,
        result.Items.Select(item => new TransactionItemResponse(
            item.Id,
            item.Position,
            item.Description,
            item.CategoryName,
            DecimalAmountMapper.Format(item.Amount))).ToArray(),
        result.Labels.Select(label => new TransactionLabelResponse(
            label.Id,
            label.Name,
            label.Scope.ToString())).ToArray());

    private static IReadOnlyList<TransactionItemInput> MapItems(
        IReadOnlyList<TransactionItemRequest> items)
        => items.Select(item => new TransactionItemInput(
            item.Description,
            item.CategoryName,
            DecimalAmountMapper.ParseTransactionAmount(item.Amount)))
            .ToArray();

    private static IReadOnlyList<TransactionLabelInput> MapLabels(
        IReadOnlyList<TransactionLabelRequest>? labels)
        => (labels ?? [])
            .Select(label => new TransactionLabelInput(
                label.Name,
                EnumParser.Parse<TransactionLabelScope>(
                    label.Scope,
                    nameof(label.Scope),
                    "Validation_TransactionLabelScopeInvalid")))
            .ToArray();
}
