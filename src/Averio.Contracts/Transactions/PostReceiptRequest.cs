using System.Text.Json.Serialization;

namespace Averio.Contracts.Transactions;

public sealed record PostReceiptRequest(
    [property: JsonRequired] Guid ExpectedVersion,
    [property: JsonRequired] string Currency,
    [property: JsonRequired] string ReviewedTotal,
    [property: JsonRequired] CreateTransactionRequest Transaction);
