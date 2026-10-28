using System.Globalization;
namespace Averio.Application.Receipts;

public static class ReceiptDraftValidation
{
    private static readonly string[] Kinds = ["Item", "Discount", "Deposit", "Tax", "Subtotal", "Payment", "Other"];
    public static ReceiptDraft Validate(ReceiptDraft draft)
    {
        if (draft is null || draft.Merchant?.Length > 200 || draft.Lines is null || draft.Lines.Length > 300
            || draft.Lines.Any(x => x is null || string.IsNullOrWhiteSpace(x.Description)
                || x.Description.Length > 500 || !Kinds.Contains(x.Kind))) throw Invalid();
        if (draft.Total is null && draft.Lines.Length == 0) throw new ReceiptExtractionException("receipt_not_found", false);
        var warnings = new List<string>();
        string? date = draft.Date;
        if (date is not null && !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out _)) { date = null; warnings.Add("date_uncertain"); }
        if (date is null) warnings.Add("date_missing");
        var currency = draft.Currency?.ToUpperInvariant();
        if (currency is not null && (currency.Length != 3 || currency.Any(c => c < 'A' || c > 'Z'))) throw Invalid();
        if (currency is not "EUR" and not "USD" and not "HUF") warnings.Add("currency_needs_review");
        var total = Amount(draft.Total);
        if (total is null || total <= 0) warnings.Add("total_needs_review");
        var lines = draft.Lines.Select(x => x with { Amount = Format(Amount(x.Amount)) }).ToArray();
        if (lines.Length == 0 || lines.Any(x => x.Amount is null)) warnings.Add("items_incomplete");
        // Keep discounts, taxes and payments as evidence, not additional ledger items.
        warnings.Add("review_required");
        return new(draft.Merchant?.Trim(), date, currency, Format(total), lines, warnings.ToArray());
    }
    private static decimal? Amount(string? text)
    {
        if (text is null) return null;
        if (text.Length > 24 || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var amount) || Math.Abs(amount) > 999999999999.99m
            || decimal.Round(amount, 2) != amount) throw Invalid();
        return amount;
    }
    private static string? Format(decimal? amount) => amount?.ToString("0.00", CultureInfo.InvariantCulture);
    private static ReceiptExtractionException Invalid() => new("invalid_extraction", false);
}
