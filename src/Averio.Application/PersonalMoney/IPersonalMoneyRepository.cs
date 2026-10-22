using Averio.Domain.PersonalMoney;

namespace Averio.Application.PersonalMoney;

public interface IPersonalMoneyRepository
{
    Task<PersonalMoneyWallet?> GetWalletAsync(Guid householdId, Guid memberId, CancellationToken ct);
    Task<PersonalMoneyBalance> GetBalanceAsync(Guid walletId, CancellationToken ct);
    Task<IReadOnlyList<PersonalMoneyEntry>> GetEntriesAsync(Guid walletId, CancellationToken ct);
    Task<IReadOnlyList<PersonalMoneyEntry>> GetTransferEntriesAsync(Guid householdId, Guid transferId, CancellationToken ct);
    Task<IReadOnlyList<PersonalMoneyEntry>> GetTransactionEntriesAsync(Guid householdId, Guid transactionId, CancellationToken ct);
    Task<IReadOnlyList<PersonalMoneyEntry>> GetRefundsOfPurchaseAsync(Guid householdId, Guid purchaseId, CancellationToken ct);
    Task<PersonalMoneyEntry?> GetByOperationAsync(Guid walletId, Guid operationId, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, string>> GetTransactionNamesAsync(
        Guid householdId, Guid memberId, Guid[] transactionIds, CancellationToken ct);
    void Add(PersonalMoneyWallet wallet);
    void Add(PersonalMoneyEntry entry);
}
