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
            DecimalAmountMapper.ParseTransactionAmount(request.Amount),
            request.Description,
            request.Note,
            request.OccurredOn);

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
            DecimalAmountMapper.ParseTransactionAmount(request.Amount),
            request.Description,
            request.Note,
            request.OccurredOn,
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
        result.Note,
        result.OccurredOn,
        result.Status.ToString(),
        result.CreatedAt,
        result.UpdatedAt,
        result.CanModify);

    public static TransactionResponse From(TransactionDetailResult result) => new(
        result.Id,
        result.AccountId,
        result.AccountName,
        result.Currency,
        result.Kind.ToString(),
        result.Visibility.ToString(),
        DecimalAmountMapper.Format(result.Amount),
        result.Description,
        result.Note,
        result.OccurredOn,
        result.Status.ToString(),
        result.CreatedAt,
        result.UpdatedAt,
        result.CanModify);
}
