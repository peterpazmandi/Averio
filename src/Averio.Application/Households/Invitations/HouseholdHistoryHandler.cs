using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;

namespace Averio.Application.Households.Invitations;

public sealed class HouseholdHistoryHandler(ICurrentUser currentUser, IHouseholdRepository households,
    IHouseholdHistoryRepository history)
{
    private Guid UserId => currentUser.UserId ?? throw new AuthenticationFailedException();
    public Task<IReadOnlyList<HistorySummary>> ListAsync(CancellationToken ct) => history.ListAsync(UserId, ct);
    public async Task<HistorySnapshot> GetAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from > to) throw new BusinessRuleException("history_range_invalid", "Error_HistoryRangeInvalid");
        var snapshot = await history.GetAsync(UserId, id, ct) ?? throw new NotFoundException("history", id);
        var rows = snapshot.Transactions.Where(x => (from == null || x.OccurredOn >= from) && (to == null || x.OccurredOn <= to)).ToArray();
        static decimal Amount(string s) => decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
        static string Money(decimal n) => n.ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture);
        return snapshot with {
            Transactions = rows,
            Transfers = snapshot.Transfers.Where(x => (from == null || x.OccurredOn >= from) && (to == null || x.OccurredOn <= to)).ToArray(),
            CashFlow = rows.Where(x => !x.IsVoided && x.IncludedInHouseholdReports).GroupBy(x => x.Currency)
                .OrderBy(x => x.Key).Select(g => new HistoryCashFlow(g.Key,
                    Money(g.Where(x => x.Kind == "Income").Sum(x => Amount(x.Amount))), Money(g.Where(x => x.Kind == "Expense").Sum(x => Amount(x.Amount))))).ToArray()
        };
    }
    public async Task<IReadOnlyList<FormerMember>> FormerMembersAsync(Guid householdId, CancellationToken ct)
    {
        _ = await households.GetByIdForUserAsync(householdId, UserId, ct)
            ?? throw new NotFoundException("household", householdId);
        return await history.FormerMembersAsync(householdId, ct);
    }
}
