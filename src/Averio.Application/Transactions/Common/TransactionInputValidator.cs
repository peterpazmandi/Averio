using Averio.Domain.Transactions;
using FluentValidation;

namespace Averio.Application.Transactions.Common;

public sealed record TransactionInput(
    Guid AccountId,
    TransactionKind Kind,
    TransactionVisibility Visibility,
    decimal Amount,
    string Description,
    string? Note,
    DateOnly OccurredOn);

public sealed class TransactionInputValidator : AbstractValidator<TransactionInput>
{
    public TransactionInputValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty()
            .WithErrorCode("Validation_TransactionAccountRequired");

        RuleFor(x => x.Kind)
            .IsInEnum()
            .WithErrorCode("Validation_TransactionKindInvalid");

        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithErrorCode("Validation_TransactionVisibilityInvalid");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .LessThanOrEqualTo(999999999999999.9999m)
            .Must(amount => decimal.Round(amount, 4) == amount)
            .WithErrorCode("Validation_TransactionAmountInvalid");

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(160)
            .WithErrorCode("Validation_TransactionDescriptionInvalid");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note is not null)
            .WithErrorCode("Validation_TransactionNoteTooLong");

        RuleFor(x => x.OccurredOn)
            .NotEmpty()
            .WithErrorCode("Validation_TransactionDateRequired");
    }
}
