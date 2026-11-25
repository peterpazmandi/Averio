using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Infrastructure;
using Averio.Infrastructure.Persistence;
using Averio.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class MoneyBackQueryChecks
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AverioDatabase"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused",
        }).Build());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
        var household = Guid.NewGuid();
        var member = Guid.NewGuid();
        _ = ItemRefundRepository.VisibleForTransaction(db, household, member, Guid.NewGuid()).ToQueryString();
        _ = ReimbursementRepository.DestinationRecoveryQuery(db, household, member).ToQueryString();
        var visible = db.VisibleTransactions(household, member).AsNoTracking();
        var allClaims = ReimbursementRepository.Visible(db, household, member).AsNoTracking();
        var allRefunds = db.Set<ItemRefund>().AsNoTracking().Where(r => r.HouseholdId == household);
        foreach (var status in new string?[] { null, "Received", "Cancelled", "Submitted" })
        {
            var claims = allClaims;
            var refunds = allRefunds;
            if (status is not null)
            {
                var parsed = Enum.Parse<ReimbursementStatus>(status);
                claims = claims.Where(c => c.Status == parsed);
                refunds = parsed switch {
                    ReimbursementStatus.Received => refunds.Where(r => r.CancelledAt == null),
                    ReimbursementStatus.Cancelled => refunds.Where(r => r.CancelledAt != null),
                    _ => refunds.Where(_ => false),
                };
            }
            _ = MoneyBackRepository.KeyQuery(claims, refunds, visible, 25, 26).ToQueryString();
            _ = MoneyBackRepository.ClaimQuery(db, claims, visible, household, member, [Guid.NewGuid()]).ToQueryString();
            _ = MoneyBackRepository.RefundQuery(db, refunds, visible, member, [Guid.NewGuid()]).ToQueryString();
        }
    }
}
