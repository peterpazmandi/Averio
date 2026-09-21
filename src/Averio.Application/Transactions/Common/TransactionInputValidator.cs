using Averio.Domain.Transactions;
using FluentValidation;

namespace Averio.Application.Transactions.Common;

public sealed record TransactionItemInput(
    string Description,
    string? CategoryName,
    decimal Amount);

public sealed record TransactionLabelInput(
    string Name,
    TransactionLabelScope Scope);

public sealed record TransactionInput(
    Guid AccountId,
    TransactionKind Kind,
    TransactionVisibility Visibility,
    string? Merchant,
    string? Note,
    DateOnly OccurredOn,
    IReadOnlyList<TransactionItemInput> Items,
    IReadOnlyList<TransactionLabelInput> Labels);

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

        RuleFor(x => x.Merchant)
            .MaximumLength(160)
            .When(x => x.Merchant is not null)
            .WithErrorCode("Validation_TransactionMerchantTooLong");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => x.Note is not null)
            .WithErrorCode("Validation_TransactionNoteTooLong");

        RuleFor(x => x.OccurredOn)
            .NotEmpty()
            .WithErrorCode("Validation_TransactionDateRequired");

        RuleFor(x => x.Items)
            .NotEmpty()
            .Must(items => items.Count <= 100)
            .WithErrorCode("Validation_TransactionItemsInvalid");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(x => x.Description)
                    .NotEmpty()
                    .MaximumLength(160)
                    .WithErrorCode("Validation_TransactionItemDescriptionInvalid");

                item.RuleFor(x => x.CategoryName)
                    .MaximumLength(80)
                    .When(x => x.CategoryName is not null)
                    .WithErrorCode("Validation_TransactionItemCategoryTooLong");

                item.RuleFor(x => x.Amount)
                    .GreaterThan(0)
                    .LessThanOrEqualTo(999999999999999.9999m)
                    .Must(amount => decimal.Round(amount, 4) == amount)
                    .WithErrorCode("Validation_TransactionAmountInvalid");
            });

        RuleFor(x => x.Items)
            .Must(items =>
            {
                var total = items.Sum(item => item.Amount);
                return total > 0 && total <= 999999999999999.9999m;
            })
            .WithErrorCode("Validation_TransactionTotalInvalid");

        RuleFor(x => x.Labels)
            .Must(labels => labels.Count <= 20)
            .WithErrorCode("Validation_TransactionLabelsTooMany")
            .Must(labels => labels
                .GroupBy(
                    label => $"{label.Scope}:{label.Name.Trim().ToUpperInvariant()}",
                    StringComparer.Ordinal)
                .All(group => group.Count() == 1))
            .WithErrorCode("Validation_TransactionLabelsDuplicate");

        RuleForEach(x => x.Labels)
            .ChildRules(label =>
            {
                label.RuleFor(x => x.Name)
                    .NotEmpty()
                    .MaximumLength(40)
                    .WithErrorCode("Validation_TransactionLabelNameInvalid");

                label.RuleFor(x => x.Scope)
                    .IsInEnum()
                    .WithErrorCode("Validation_TransactionLabelScopeInvalid");
            });
    }
}
