using Averio.Application.PersonalMoney;
using Averio.Domain.PersonalMoney;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class PersonalMoneyRepository(AverioDbContext db) : IPersonalMoneyRepository
{
    public Task<PersonalMoneyWallet?> GetWalletAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => db.Set<PersonalMoneyWallet>().SingleOrDefaultAsync(x =>
            x.HouseholdId == householdId && x.HouseholdMemberId == memberId, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetTransactionNamesAsync(
        Guid householdId, Guid memberId, Guid[] transactionIds, CancellationToken ct)
    {
        if (transactionIds.Length == 0) return new Dictionary<Guid, string>();
        // Ledger access must not reveal a now-private purchase name.
        return await db.VisibleTransactions(householdId, memberId).AsNoTracking()
            .Where(x => transactionIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Description })
            .ToDictionaryAsync(x => x.Id, x => x.Description, ct);
    }

    // Aggregate in PostgreSQL: summary reads return one row regardless of ledger size.
    public async Task<PersonalMoneyBalance> GetBalanceAsync(Guid walletId, CancellationToken ct)
    {
        var balance = await db.Set<PersonalMoneyEntry>().AsNoTracking()
            .Where(x => x.WalletId == walletId).GroupBy(x => x.WalletId)
            .Select(g => new PersonalMoneyBalance(
                g.Sum(x => x.Kind == PersonalMoneyEntryKind.Grant
                    || x.Kind == PersonalMoneyEntryKind.PrivateCharge
                    || x.Kind == PersonalMoneyEntryKind.PrivateRefund
                    || x.Kind == PersonalMoneyEntryKind.SharedCharge
                    || x.Kind == PersonalMoneyEntryKind.SharedRefund
                    || x.Kind == PersonalMoneyEntryKind.Surrender ? x.Amount : 0m),
                g.Sum(x => x.Kind == PersonalMoneyEntryKind.Funding
                    || x.Kind == PersonalMoneyEntryKind.PrivateCharge
                    || x.Kind == PersonalMoneyEntryKind.PrivateRefund ? x.Amount : 0m)))
            .SingleOrDefaultAsync(ct);
        return balance ?? new PersonalMoneyBalance(0m, 0m);
    }

    public async Task<IReadOnlyList<PersonalMoneyEntry>> GetEntriesAsync(Guid walletId, CancellationToken ct)
        => await db.Set<PersonalMoneyEntry>().AsNoTracking().Where(x => x.WalletId == walletId)
            .OrderBy(x => x.OccurredOn).ThenBy(x => x.RecordedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<PersonalMoneyEntry>> GetTransferEntriesAsync(Guid householdId,
        Guid transferId, CancellationToken ct)
        => await db.Set<PersonalMoneyEntry>().AsNoTracking().Where(x =>
            x.HouseholdId == householdId && x.TransferId == transferId).ToListAsync(ct);

    public async Task<IReadOnlyList<PersonalMoneyEntry>> GetTransactionEntriesAsync(Guid householdId,
        Guid transactionId, CancellationToken ct)
        => await db.Set<PersonalMoneyEntry>().AsNoTracking().Where(x =>
            x.HouseholdId == householdId && x.TransactionId == transactionId).ToListAsync(ct);

    public async Task<IReadOnlyList<PersonalMoneyEntry>> GetRefundsOfPurchaseAsync(Guid householdId, Guid purchaseId, CancellationToken ct)
        => await db.Set<PersonalMoneyEntry>().AsNoTracking().Where(x =>
            x.HouseholdId == householdId && x.RelatedTransactionId == purchaseId).ToListAsync(ct);

    public Task<PersonalMoneyEntry?> GetByOperationAsync(Guid walletId, Guid operationId, CancellationToken ct)
        => db.Set<PersonalMoneyEntry>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.WalletId == walletId && x.OperationId == operationId, ct);

    public void Add(PersonalMoneyWallet wallet) => db.Set<PersonalMoneyWallet>().Add(wallet);
    public void Add(PersonalMoneyEntry entry) => db.Set<PersonalMoneyEntry>().Add(entry);
}
