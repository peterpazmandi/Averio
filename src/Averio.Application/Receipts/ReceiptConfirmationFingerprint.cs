using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Averio.Application.Transactions.CreateTransaction;

namespace Averio.Application.Receipts;

// Immutable confirmation intent, independent of the subsequently editable ledger.
public static class ReceiptConfirmationFingerprint
{
    public static string Create(Guid expectedVersion, string currency, decimal reviewedTotal,
        CreateTransactionCommand command)
    {
        var payload = new
        {
            FormatVersion = 1,
            ExpectedVersion = expectedVersion,
            Currency = currency,
            ReviewedTotal = Amount(reviewedTotal),
            command.HouseholdId,
            command.AccountId,
            Kind = command.Kind.ToString(),
            Visibility = command.Visibility.ToString(),
            Merchant = Text(command.Merchant),
            Note = Text(command.Note),
            command.OccurredOn,
            Items = command.Items.Select(item => new
            {
                Description = item.Description?.Trim(),
                item.CategoryId,
                Amount = Amount(item.Amount),
                item.ExistingItemId,
                PersonalMoneyAmount = item.PersonalMoneyAmount is decimal personal ? Amount(personal) : null,
            }).ToArray(),
            // Label order has no financial meaning; receipt item order does.
            LabelIds = command.LabelIds.Order().ToArray(),
            command.ActivityAssignment,
            command.ChargePersonalMoney,
        };
        return Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(payload)));
    }

    private static string Amount(decimal amount) => amount.ToString("0.############################", CultureInfo.InvariantCulture);
    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
