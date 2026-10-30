using System.Text.Json.Serialization;

namespace Averio.Contracts.Transactions;

public sealed record PostReceiptRequest(
    [property: JsonRequired] Guid ExpectedVersion,
    [property: JsonRequired] string Currency,
    [property: JsonRequired] string ReviewedTotal,
    [property: JsonRequired] CreateTransactionRequest Transaction,
    [property: System.ComponentModel.DataAnnotations.MaxLength(100)] RememberReceiptCategoryRequest[]? RememberCategoryRules = null);

public sealed record RememberReceiptCategoryRequest(
    [property: JsonRequired] int LineIndex, [property: JsonRequired] int ItemIndex);

