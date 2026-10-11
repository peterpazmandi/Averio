using System.Text.Json;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Households.Invitations;
using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(AverioDbContext db, IAccountBalanceReader balances, TimeProvider clock) : IInvitationRepository
{
    public async Task LockUserAsync(Guid userId, CancellationToken ct)
        => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM users WHERE id = {userId} FOR UPDATE", ct);
    public async Task<Household?> LockHouseholdAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Transaction required.");
        var rows = await db.Households.FromSqlInterpolated($"SELECT * FROM households WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var h = rows.SingleOrDefault();
        if (h is not null)
        {
            await db.Entry(h).ReloadAsync(ct);
            await db.Entry(h).Collection(x => x.Members).LoadAsync(ct);
        }
        return h;
    }
    public async Task<HouseholdInvitation?> FindAsync(Guid id, CancellationToken ct)
    {
        var row = await db.Set<HouseholdInvitation>().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row;
    }
    public async Task<HouseholdInvitation?> FindByHashAsync(string hash, CancellationToken ct)
    {
        var row = await db.Set<HouseholdInvitation>().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct);
        return row is not null && row.TokenHash == hash ? row : null;
    }
    public async Task<IReadOnlyList<HouseholdInvitation>> ListAsync(Guid householdId, CancellationToken ct)
        => await db.Set<HouseholdInvitation>().Where(x => x.HouseholdId == householdId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public async Task<IReadOnlyList<PendingInvitation>> PendingAsync(string normalizedEmail, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return await (from i in db.Set<HouseholdInvitation>().AsNoTracking()
            join h in db.Households on i.HouseholdId equals h.Id
            where i.NormalizedEmail == normalizedEmail && i.State == "Pending" && i.ExpiresAt > now
                && h.ArchivedAtUtc == null && h.FinancialSetupCompletedAtUtc != null
                && db.HouseholdMembers.Any(m => m.HouseholdId == h.Id && m.UserId == i.InviterUserId && m.Role == HouseholdRole.Owner)
            orderby i.CreatedAt descending
            select new PendingInvitation(i.Id, h.Name!)).ToListAsync(ct);
    }
    public void Add(HouseholdInvitation invitation) => db.Add(invitation);
    private Task<List<Account>> VisibleAccountsAsync(Guid householdId, Guid memberId, CancellationToken ct)
        => db.Accounts.Include(x => x.Owners).Where(x => x.HouseholdId == householdId
            && (x.Visibility == AccountVisibility.Household || x.Owners.Any(o => o.HouseholdMemberId == memberId)))
            .OrderBy(x => x.Name).ThenBy(x => x.Id).ToListAsync(ct);
    private static bool Eligible(Account a, Guid memberId) => a.Status == AccountStatus.Active
        && a.CutoverAtUtc == null && a.Scope == AccountScope.Personal
        && a.Owners.Count == 1 && a.Owners.Single().HouseholdMemberId == memberId;
    public async Task<DeparturePreview> DepartureAsync(Household source, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var member = source.Members.SingleOrDefault(x => x.UserId == userId && x.LeftAtUtc is null)
            ?? throw new ConflictException("departure_changed", "Error_DepartureChanged");
        var accounts = await VisibleAccountsAsync(source.Id, member.Id, ct);
        var eligible = accounts.Where(x => Eligible(x, member.Id)).ToArray();
        var amounts = await balances.GetCurrentBalancesAsync(source.Id, member.Id, eligible.Select(x => x.Id).ToArray(),
            DateOnly.FromDateTime(now.UtcDateTime), ct);
        var others = source.Members.Where(x => x.Id != member.Id && x.LeftAtUtc is null).ToArray();
        var names = await db.Users.Where(x => others.Select(m => m.UserId).Contains(x.Id))
            .Select(x => new { x.Id, x.DisplayName }).ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);
        return new(source.Id, source.Name, source.Version, others.Length == 0,
            member.Role == HouseholdRole.Owner && others.Length > 0, accounts.Count - eligible.Length,
            eligible.Select(x => new DepartureAccount(x.Id, x.Name, x.Currency.Value, x.Type.ToString(), x.UpdatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture), amounts[x.Id])).ToArray(),
            others.Select(x => new DepartureMember(x.Id, names.GetValueOrDefault(x.UserId))).ToArray());
    }
    public async Task JoinAsync(Household source, Household target, Guid userId, InvitationAcceptance input, DateTimeOffset now, CancellationToken ct)
    {
        var plan = await DepartureAsync(source, userId, now, ct);
        if (input.Accounts is null || input.Accounts.Count != plan.Accounts.Count
            || input.Accounts.Select(x => x.AccountId).Distinct().Count() != input.Accounts.Count
            || input.Accounts.Any(x => x.Handling is not ("private" or "shared" or "household"))
            || plan.Accounts.Any(x => !input.Accounts.Any(c => c.AccountId == x.Id && c.ExpectedVersion == x.Version && c.OpeningBalance == x.Balance))
            || (plan.RequiresSuccessor && !plan.Successors.Any(x => x.Id == input.SuccessorMemberId)))
            throw new ConflictException("departure_changed", "Error_DepartureChanged");
        var member = source.Members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
        var visibleAccounts = await VisibleAccountsAsync(source.Id, member.Id, ct);
        var amounts = await balances.GetCurrentBalancesAsync(source.Id, member.Id, visibleAccounts.Select(x => x.Id).ToArray(),
            DateOnly.FromDateTime(now.UtcDateTime), ct);
        var visibleTransactions = await db.VisibleTransactions(source.Id, member.Id).AsNoTracking().Include(x => x.Items)
            .OrderByDescending(x => x.OccurredOn).ThenBy(x => x.Id).ToListAsync(ct);
        var visibleIds = visibleAccounts.Select(x => x.Id).ToHashSet();
        var transfers = await db.Transfers.AsNoTracking().Where(x => x.HouseholdId == source.Id
            && (visibleIds.Contains(x.SourceAccountId) || visibleIds.Contains(x.DestinationAccountId)))
            .OrderByDescending(x => x.OccurredOn).ThenBy(x => x.Id).ToListAsync(ct);
        // Freeze classification at cutover; history reads never need the source
        // category directory, and custom names may equal a built-in system key.
        var categoryIds = visibleTransactions.SelectMany(x => x.Items).Select(x => x.CategoryId).Distinct().ToArray();
        var systemCategoryIds = (await db.Categories.AsNoTracking()
            .Where(x => x.IsBase && categoryIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var accountById = visibleAccounts.ToDictionary(x => x.Id);
        static string Money(decimal value) => value.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
        var snapshot = new HistorySnapshot(new(Guid.Empty, source.Name ?? "Averio", now, plan.WillArchive),
            visibleAccounts.Select(x => new HistoryAccount(x.Id, x.Name, x.Currency.Value, x.Type.ToString(), x.Scope.ToString(),
                x.Visibility.ToString(), x.IncludeInHouseholdReports, Money(amounts[x.Id]), plan.Accounts.Any(a => a.Id == x.Id))).ToArray(),
            visibleTransactions.Select(x => new HistoryTransaction(x.Id, x.AccountId, x.Description, x.Merchant, x.Note, x.OccurredOn,
                Money(x.Amount), x.Currency.Value, x.Kind.ToString(), x.Status == TransactionStatus.Voided,
                accountById[x.AccountId].IncludeInHouseholdReports && x.Visibility == TransactionVisibility.Household,
                x.Items.OrderBy(i => i.Position).Select(i => new HistoryItem(i.Description, i.CategoryName, Money(i.Amount), systemCategoryIds.Contains(i.CategoryId))).ToArray())).ToArray(),
            transfers.Select(x => new HistoryTransfer(x.Id, x.OccurredOn,
                visibleIds.Contains(x.SourceAccountId) ? new(x.SourceAccountId, accountById[x.SourceAccountId].Name, x.SourceCurrency.Value, Money(x.SentAmount)) : null,
                visibleIds.Contains(x.DestinationAccountId) ? new(x.DestinationAccountId, accountById[x.DestinationAccountId].Name, x.DestinationCurrency.Value, Money(x.ReceivedAmount)) : null,
                visibleIds.Contains(x.SourceAccountId) && visibleIds.Contains(x.DestinationAccountId) ? x.Note : null, x.VoidedAt != null)).ToArray(), []);
        if (source.Name is not null || visibleAccounts.Count > 0 || visibleTransactions.Count > 0 || transfers.Count > 0)
            db.Add(HouseholdHistory.Create(source.Id, userId, source.Name ?? "Averio", now, plan.WillArchive, JsonSerializer.Serialize(snapshot)));
        foreach (var old in visibleAccounts.Where(x => Eligible(x, member.Id))) old.CloseForDeparture(now);
        source.Depart(userId, input.SuccessorMemberId, now);
        // Flush departure first, releasing the filtered active-user/owner unique indexes.
        // Both flushes and the snapshot/copies are inside the caller's transaction.
        await db.SaveChangesAsync(ct);
        if (plan.RequiresSuccessor) source.PromoteSuccessor(input.SuccessorMemberId!.Value);
        target.AddMember(userId, now);
        var newMember = target.Members.Single(x => x.UserId == userId && x.LeftAtUtc is null);
        db.Entry(newMember).State = EntityState.Added;
        foreach (var choice in input.Accounts)
        {
            var old = accountById[choice.AccountId];
            var householdOwned = choice.Handling == "household";
            var privateAccount = choice.Handling == "private";
            db.Accounts.Add(Account.Create(target.Id, [newMember.Id], old.Name, old.Type,
                householdOwned ? AccountScope.Household : AccountScope.Personal,
                privateAccount ? AccountVisibility.Private : AccountVisibility.Household,
                !privateAccount, old.Currency, choice.OpeningBalance, DateOnly.FromDateTime(now.UtcDateTime), now));
        }
    }
    public async Task RecordDeliveryAsync(Guid id, Guid version, bool sent, CancellationToken ct)
        => await db.Set<HouseholdInvitation>().Where(x => x.Id == id && x.Version == version)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeliveryStatus, sent ? "Sent" : "Failed"), ct);
}
