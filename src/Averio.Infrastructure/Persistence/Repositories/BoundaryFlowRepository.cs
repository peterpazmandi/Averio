using Averio.Application.Statistics.GetBoundaryCashFlow;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

// Report only aggregated movement crossing the household's financial reporting boundary.
// Account IDs, transfer IDs, notes and private counterparties never leave this repository.
internal sealed class BoundaryFlowRepository(AverioDbContext db) : IBoundaryFlowRepository
{
    public async Task<IReadOnlyList<BoundaryFlowMonth>> GetMonthsAsync(Guid householdId,
        DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var rows = await (from transfer in db.Transfers.AsNoTracking()
            join source in db.Accounts.AsNoTracking()
                on new { transfer.HouseholdId, Id = transfer.SourceAccountId }
                equals new { source.HouseholdId, source.Id }
            join destination in db.Accounts.AsNoTracking()
                on new { transfer.HouseholdId, Id = transfer.DestinationAccountId }
                equals new { destination.HouseholdId, destination.Id }
            where transfer.HouseholdId == householdId
                && transfer.VoidedAt == null
                && transfer.OccurredOn >= fromDate && transfer.OccurredOn <= toDate
                && source.IncludeInHouseholdReports != destination.IncludeInHouseholdReports
            select new
            {
                transfer.OccurredOn,
                transfer.SentAmount,
                transfer.ReceivedAmount,
                transfer.SourceCurrency,
                transfer.DestinationCurrency,
                SourceIncluded = source.IncludeInHouseholdReports
            }).ToListAsync(ct);

        return rows.Select(x => new
            {
                Currency = x.SourceIncluded ? x.SourceCurrency.Value : x.DestinationCurrency.Value,
                Month = new DateOnly(x.OccurredOn.Year, x.OccurredOn.Month, 1),
                Out = x.SourceIncluded ? x.SentAmount : 0m,
                In = !x.SourceIncluded ? x.ReceivedAmount : 0m,
            })
            .GroupBy(x => new { x.Currency, x.Month })
            .Select(g => new BoundaryFlowMonth(g.Key.Currency, g.Key.Month,
                g.Sum(x => x.Out), g.Sum(x => x.In), g.Count()))
            .OrderBy(x => x.Month).ThenBy(x => x.Currency).ToArray();
    }
}
