using System.Text.Json;
using Averio.Application.Households.Invitations;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class HouseholdHistoryRepository(AverioDbContext db) : IHouseholdHistoryRepository
{
    public async Task<IReadOnlyList<HistorySummary>> ListAsync(Guid userId, CancellationToken ct)
        => await db.Set<HouseholdHistory>().AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.LeftAtUtc)
            .Select(x => new HistorySummary(x.Id, x.HouseholdName, x.LeftAtUtc, x.WasLastMember)).ToListAsync(ct);
    public async Task<HistorySnapshot?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = await db.Set<HouseholdHistory>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (row is null) return null;
        var data = JsonSerializer.Deserialize<HistorySnapshot>(row.SnapshotJson)!;
        return data with { Summary = new(row.Id, row.HouseholdName, row.LeftAtUtc, row.WasLastMember) };
    }
    public async Task<IReadOnlyList<FormerMember>> FormerMembersAsync(Guid householdId, CancellationToken ct)
        => await (from m in db.HouseholdMembers.IgnoreQueryFilters().AsNoTracking()
            join u in db.Users on m.UserId equals u.Id
            where m.HouseholdId == householdId && m.LeftAtUtc != null
            orderby m.LeftAtUtc descending
            select new FormerMember(m.Id, u.DisplayName, m.LeftAtUtc!.Value)).ToListAsync(ct);
}
