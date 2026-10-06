using Averio.Application.Households.Invitations;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class InvitationRepository(AverioDbContext db) : IInvitationRepository
{
    public async Task LockUserAsync(Guid userId, CancellationToken ct)
        => await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM users WHERE id = {userId} FOR UPDATE", ct);
    public async Task<Household?> LockHouseholdAsync(Guid id, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Transaction required.");
        var rows = await db.Households.FromSqlInterpolated($"SELECT * FROM households WHERE id = {id} FOR UPDATE").ToListAsync(ct);
        var h = rows.SingleOrDefault();
        if (h is not null) await db.Entry(h).Collection(x => x.Members).LoadAsync(ct);
        return h;
    }
    public Task<HouseholdInvitation?> FindAsync(Guid id, CancellationToken ct)
        => db.Set<HouseholdInvitation>().SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<HouseholdInvitation?> FindByHashAsync(string hash, CancellationToken ct)
    {
        var row = await db.Set<HouseholdInvitation>().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (row is not null) await db.Entry(row).ReloadAsync(ct); // Revalidate after acquiring household locks.
        return row is not null && row.TokenHash == hash ? row : null;
    }
    public async Task<IReadOnlyList<HouseholdInvitation>> ListAsync(Guid householdId, CancellationToken ct)
        => await db.Set<HouseholdInvitation>().Where(x => x.HouseholdId == householdId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
    public void Add(HouseholdInvitation invitation) => db.Add(invitation);
    public async Task<bool> IsEmptySoloAsync(Household h, Guid userId, CancellationToken ct)
    {
        if (h.Members.Count != 1 || !h.Members.Any(x => x.UserId == userId && x.Role == HouseholdRole.Owner)) return false;
        var member = h.Members.Single();
        if (db.Database.CurrentTransaction is not null)
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM household_members WHERE id = {member.Id} FOR UPDATE", ct);

        // Check EVERY mapped household/member dependent, including archived/voided rows.
        // FK key-share locks on inserts conflict with the FOR UPDATE locks during acceptance;
        // concurrent inserts either commit before these reads or fail after source deletion.
        foreach (var entity in db.Model.GetEntityTypes())
        {
            if (entity.ClrType == typeof(HouseholdMember)) continue;
            foreach (var fk in entity.GetForeignKeys().Where(x => x.PrincipalEntityType.ClrType == typeof(Household)
                || x.PrincipalEntityType.ClrType == typeof(HouseholdMember)))
            {
                var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
                var values = fk.PrincipalKey.Properties.Select(p => p.Name switch {
                    "Id" => fk.PrincipalEntityType.ClrType == typeof(Household) ? h.Id : member.Id,
                    "HouseholdId" => h.Id,
                    _ => throw new InvalidOperationException("Unreviewed household dependency key.")
                }).ToArray();
                static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
                var where = string.Join(" AND ", fk.Properties.Select((p, i) => Quote(p.GetColumnName(table)!) + " = {" + i + "}"));
                var sql = "SELECT EXISTS(SELECT 1 FROM " + (table.Schema is null ? "" : Quote(table.Schema) + ".") + Quote(table.Name) + " WHERE " + where + ") AS \"Value\"";
                if (await db.Database.SqlQueryRaw<bool>(sql, values.Cast<object>().ToArray()).SingleAsync(ct)) return false;
            }
        }
        return true;
    }
    public async Task JoinAsync(Household source, Household target, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        // Both parents and source membership are locked, all dependents checked before deletion.
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM household_members WHERE household_id = {source.Id} AND user_id = {userId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM households WHERE id = {source.Id}", ct);
        foreach (var member in source.Members) db.Entry(member).State = EntityState.Detached;
        db.Entry(source).State = EntityState.Detached;
        target.AddMember(userId, now);
        // Existing target aggregate is tracked, but a new Guid key must be explicitly Added.
        db.Entry(target.Members.Single(x => x.UserId == userId)).State = EntityState.Added;
    }
    public async Task RecordDeliveryAsync(Guid id, Guid version, bool sent, CancellationToken ct)
        => await db.Set<HouseholdInvitation>().Where(x => x.Id == id && x.Version == version)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DeliveryStatus, sent ? "Sent" : "Failed"), ct);
}
