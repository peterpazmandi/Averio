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
            request.LabelIds ?? [], MapAssignment(request.ActivityAssignment), request.ChargePersonalMoney);

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
            request.LabelIds ?? [],
            request.ExpectedUpdatedAt, MapAssignment(request.ActivityAssignment));

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
            item.CategoryId,
            item.CategoryName,
            item.CategoryIcon,
            DecimalAmountMapper.Format(item.Amount))).ToArray(),
        result.Labels.Select(label => new TransactionLabelResponse(
            label.Id,
            label.Name,
            label.Scope.ToString())).ToArray(),
        result.ActivityId, result.ActivityName, result.ActivityAssignmentVersion, result.IsArchived);

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
            item.CategoryId,
            item.CategoryName,
            item.CategoryIcon,
            DecimalAmountMapper.Format(item.Amount))).ToArray(),
        result.Labels.Select(label => new TransactionLabelResponse(
            label.Id,
            label.Name,
            label.Scope.ToString())).ToArray(),
        result.ActivityId, result.ActivityName, result.ActivityAssignmentVersion, result.IsArchived);

    private static Averio.Application.Activities.ActivityAssignmentInput? MapAssignment(Averio.Contracts.Activities.ActivityAssignmentRequest? input)
        => input is null ? null : new(input.ActivityId, input.ExpectedVersion);

    private static IReadOnlyList<TransactionItemInput> MapItems(
        IReadOnlyList<TransactionItemRequest> items)
        => items.Select(item => new TransactionItemInput(
            item.Description,
            item.CategoryId,
            DecimalAmountMapper.ParseTransactionAmount(item.Amount), item.ExistingItemId,
            item.PersonalMoneyAmount is null ? null : DecimalAmountMapper.ParseTransactionAmount(item.PersonalMoneyAmount)))
            .ToArray();
}


