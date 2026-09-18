namespace Averio.Domain.Common;

public readonly record struct CurrencyCode
{
    public static readonly CurrencyCode Eur = new("EUR");
    public static readonly CurrencyCode Usd = new("USD");
    public static readonly CurrencyCode Huf = new("HUF");

    private static readonly HashSet<string> SupportedCodes =
    [
        "EUR",
        "USD",
        "HUF",
    ];

    public string Value { get; }

    private CurrencyCode(string value)
    {
        Value = value;
    }

    public static bool IsSupported(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && SupportedCodes.Contains(value.Trim().ToUpperInvariant());
    }

    public static CurrencyCode From(string value)
    {
        var normalized = value
            .Trim()
            .ToUpperInvariant();

        if (!SupportedCodes.Contains(normalized))
        {
            throw new ArgumentException(
                $"Unsupported currency code '{value}'.",
                nameof(value));
        }

        return new CurrencyCode(normalized);
    }

    public override string ToString()
    {
        return Value;
    }
}