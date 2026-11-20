using System.Text.Json.Serialization;
namespace Averio.Contracts.Transactions;
public sealed record RecordItemRefundRequest(Guid TransactionItemId, [property: JsonRequired] string Amount,
    DateOnly ReceivedOn, Guid OperationId, DateTimeOffset ExpectedUpdatedAt, [property: JsonRequired] bool MoneyReceived,
    Guid? CreditTransactionId = null, DateTimeOffset? ExpectedCreditUpdatedAt = null);
public sealed record RecordItemRefundResponse(Guid Id);
public sealed record CancelItemRefundRequest(Guid ExpectedVersion, DateTimeOffset ExpectedUpdatedAt);
public sealed record ItemRefundResponse(Guid Id, Guid TransactionId, Guid TransactionItemId, Guid CreditTransactionId,
    string Amount, DateOnly ReceivedOn, DateTimeOffset CreatedAt, DateTimeOffset? CancelledAt, Guid Version, string CreditOrigin);
