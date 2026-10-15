namespace Averio.Contracts.Transactions;
public sealed record SavedViewFilterRequest(Guid? Account, string? Kind, string Period, DateOnly? From, DateOnly? To, Guid? Activity, Guid[] Labels);
public sealed record SaveTransactionViewRequest(string Name, SavedViewFilterRequest Filters, Guid? ExpectedVersion);
public sealed record SavedTransactionViewResponse(Guid Id, string Name, SavedViewFilterRequest Filters, Guid Version, bool Available);
