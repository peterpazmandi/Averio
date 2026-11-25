using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.Refunds;

namespace Averio.Application.Recurring;
public sealed class MoneyBackHandler(ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ITransactionRepository transactions, IReimbursementRepository reimbursements, ItemRefundHandler refunds)
{
    public async Task<(IReadOnlyList<MoneyBackRow> Items, bool HasMore)> ListAsync(Guid householdId, int offset, int limit, string? status, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct) ?? throw new NotFoundException("household", householdId);
        var member = household.Members.Single(x => x.UserId == userId && x.LeftAtUtc == null);
        var includeCancelled = status is null || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
        var claims = await reimbursements.ListAsync(householdId, member.Id, null, ct, includeCancelled);
        var rows = new List<MoneyBackRow>();
        foreach (var claim in claims.Where(c => status is null || c.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)))
        {
            var payment = await transactions.GetVisibleByIdAsync(householdId, member.Id, claim.TransactionId, ct);
            var account = payment is null
                ? null
                : await accounts.GetVisibleByIdAsync(householdId, member.Id, payment.AccountId, ct);
            var canModify = account is not null && TransactionAccess.CanModify(account, member);
            rows.Add(new MoneyBackRow("Reimbursement", claim.Id, claim.Provider, null, claim.Currency,
                claim.ExpectedAmount, claim.OutstandingAmount, claim.PaymentOn, claim.Status.ToString(),
                claim.TransactionId, null, canModify));
        }
        if (status is null || status.Equals("Received", StringComparison.OrdinalIgnoreCase) || status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            var refundSkip = 0;
            while (true)
            {
                var refundRows = await refunds.OverviewAsync(householdId, refundSkip, 100, ct);
                rows.AddRange(refundRows.Where(r => status is null
                        || (status.Equals("Received", StringComparison.OrdinalIgnoreCase) && !r.Cancelled)
                        || (status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) && r.Cancelled))
                    .Select(r => new MoneyBackRow("Refund", r.Id, null, r.Merchant, r.Currency, r.Amount, null,
                        r.ReceivedOn, r.Cancelled ? "Cancelled" : "Received", r.PurchaseId, r.CreditId, r.CanModify)));
                if (refundRows.Count < 100) break;
                refundSkip += 100;
            }
        }
        var size = Math.Clamp(limit, 1, 100); var page = rows.OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).Skip(Math.Max(0, offset)).Take(size + 1).ToArray();
        return (page.Take(size).ToArray(), page.Length > size);
    }
    public sealed record MoneyBackRow(string Kind, Guid Id, string? Provider, string? Merchant, string Currency, decimal Amount, decimal? Outstanding, DateOnly Date, string Status, Guid? SourceId, Guid? CreditId, bool CanModify);
}
