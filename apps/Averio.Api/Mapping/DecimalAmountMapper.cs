using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;

namespace Averio.Api.Mapping;

internal static partial class DecimalAmountMapper
{
    public static decimal ParseBalance(string? value)
    {
        if (value is not null
            && BalancePattern().IsMatch(value)
            && decimal.TryParse(
                value,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return amount;
        }

        const string errorCode = "Validation_AccountBalanceInvalid";

        throw new ValidationException(
        [
            new ValidationFailure("BalanceAnchorAmount", errorCode)
            {
                ErrorCode = errorCode
            }
        ]);
    }

    public static decimal ParseTransactionAmount(string? value)
    {
        if (value is not null
            && TransactionAmountPattern().IsMatch(value)
            && decimal.TryParse(
                value,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            return amount;
        }

        const string errorCode = "Validation_TransactionAmountInvalid";

        throw new ValidationException(
        [
            new ValidationFailure("Amount", errorCode)
            {
                ErrorCode = errorCode
            }
        ]);
    }

    public static string Format(decimal amount)
    {
        return amount.ToString(
            "0.############################",
            CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"\A(?:0|[1-9][0-9]{0,14})(?:\.[0-9]{1,4})?\z")]
    private static partial Regex TransactionAmountPattern();

    [GeneratedRegex(@"\A-?(?:0|[1-9][0-9]{0,14})(?:\.[0-9]{1,4})?\z")]
    private static partial Regex BalancePattern();
}
