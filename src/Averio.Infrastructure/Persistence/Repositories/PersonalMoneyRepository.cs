using Averio.Application.PersonalMoney;
using Averio.Domain.PersonalMoney;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class PersonalMoneyRepository(AverioDbContext db) : IPersonalMoneyRepository
{
    public Task<PersonalMoneyWallet?> GetWalletAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => db.Set<PersonalMoneyWallet>().SingleOrDefaultAsync(x =>
            x.HouseholdId == householdId && x.HouseholdMemberId == memberId, ct);

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

    public Task<PersonalMoneyEntry?> GetByOperationAsync(Guid walletId, Guid operationId, CancellationToken ct)
        => db.Set<PersonalMoneyEntry>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.WalletId == walletId && x.OperationId == operationId, ct);

    public void Add(PersonalMoneyWallet wallet) => db.Set<PersonalMoneyWallet>().Add(wallet);
    public void Add(PersonalMoneyEntry entry) => db.Set<PersonalMoneyEntry>().Add(entry);
}
