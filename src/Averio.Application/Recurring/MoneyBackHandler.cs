using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Refunds;

namespace Averio.Application.Recurring;
public sealed class MoneyBackHandler(ICurrentUser user, IHouseholdRepository households, IReimbursementRepository reimbursements, ItemRefundHandler refunds)
{
    public async Task<(IReadOnlyList<MoneyBackRow> Items, bool HasMore)> ListAsync(Guid householdId, int offset, int limit, string? status, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct) ?? throw new NotFoundException("household", householdId);
        var member = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc == null);
        var claims = await reimbursements.ListAsync(householdId, member.Id, null, ct);
        var rows = claims.Where(c => status is null || c.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
            .Select(c => new MoneyBackRow("Reimbursement", c.Id, c.Provider, null, c.Currency, c.ExpectedAmount, c.OutstandingAmount, c.PaymentOn, c.Status.ToString(), c.TransactionId, null, true)).ToList();
        if (status is null || status.Equals("Received", StringComparison.OrdinalIgnoreCase) || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            var refundRows = await refunds.OverviewAsync(householdId, 0, 101, ct);
            rows.AddRange(refundRows.Where(r => status is null || (status.Equals("Received", StringComparison.OrdinalIgnoreCase) && !r.Cancelled) || (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) && r.Cancelled)).Select(r => new MoneyBackRow("Refund", r.Id, null, r.Merchant, r.Currency, r.Amount, null, r.ReceivedOn, r.Cancelled ? "Cancelled" : "Received", r.PurchaseId, r.CreditId, r.CanModify)));
        }
        var size = Math.Clamp(limit, 1, 100); var page = rows.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Skip(Math.Max(0, offset)).Take(size + 1).ToArray();
        return (page.Take(size).ToArray(), page.Length > size);
    }
    public sealed record MoneyBackRow(string Kind, Guid Id, string? Provider, string? Merchant, string Currency, decimal Amount, decimal? Outstanding, DateOnly Date, string Status, Guid? SourceId, Guid? CreditId, bool CanModify);
}
