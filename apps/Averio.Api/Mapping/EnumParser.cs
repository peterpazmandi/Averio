using FluentValidation;
using FluentValidation.Results;

namespace Averio.Api.Mapping;

internal static class EnumParser
{
    public static TEnum Parse<TEnum>(
        string? value,
        string propertyName,
        string errorCode)
        where TEnum : struct, Enum
    {
        var normalized = value?.Trim();

        if (normalized is not null
            && Enum.GetNames<TEnum>().Contains(
                normalized,
                StringComparer.OrdinalIgnoreCase))
        {
            return Enum.Parse<TEnum>(
                normalized,
                ignoreCase: true);
        }

        throw new ValidationException(
        [
            new ValidationFailure(
                propertyName,
                errorCode)
            {
                ErrorCode = errorCode
            }
        ]);
    }
}