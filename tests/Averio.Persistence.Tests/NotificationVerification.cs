using Averio.Application.Notifications;
using Averio.Domain.Categories;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class NotificationVerification
{
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        var ct = CancellationToken.None;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        Guid ruleId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var category = await db.Categories.FirstAsync(c => c.IsBase && c.Kind == CategoryKind.Expense, ct);
            var rule = RecurringRule.Create(f.HouseholdId, f.OwnerMemberId, f.HouseholdAccountId, category.Id,
                "Private reminder verification", 10m, TransactionKind.Expense, TransactionVisibility.Household,
                RecurringFrequency.Monthly, today, null, false, clock.GetUtcNow());
            db.Add(rule);
            await db.SaveChangesAsync(ct);
            ruleId = rule.Id;
        }

        user.UserId = f.OwnerUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<INotificationInbox>();
            var first = (await inbox.ListAsync(f.OwnerUserId, f.HouseholdId, false, ct))
                .Single(n => n.TargetId == ruleId && n.Type == "RecurringConfirmationDue");
            var second = await inbox.ListAsync(f.OwnerUserId, f.HouseholdId, false, ct);
            if (second.Count(n => n.TargetId == ruleId) != 1) throw new Exception("Reminder reconciliation duplicated an occurrence.");
            if (!await inbox.MarkReadAsync(f.OwnerUserId, first.Id, ct)) throw new Exception("Owner could not read reminder.");
            if ((await inbox.ListAsync(f.OwnerUserId, f.HouseholdId, false, ct)).Single(n => n.Id == first.Id).ReadAt is null)
                throw new Exception("Read state did not persist.");
            if (!await inbox.DismissAsync(f.OwnerUserId, first.Id, ct) ||
                (await inbox.ListAsync(f.OwnerUserId, f.HouseholdId, false, ct)).Any(n => n.Id == first.Id))
                throw new Exception("Dismissed reminder returned after reconciliation.");
        }

        user.UserId = f.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var inbox = scope.ServiceProvider.GetRequiredService<INotificationInbox>();
            if ((await inbox.ListAsync(f.MemberUserId, f.HouseholdId, false, ct)).Any(n => n.TargetId == ruleId))
                throw new Exception("Another household member received a private schedule reminder.");
            if (await inbox.MarkReadAsync(f.MemberUserId, (await RuleReminderId()).GetValueOrDefault(), ct))
                throw new Exception("Another member changed a private reminder.");
        }
        user.UserId = f.OwnerUserId;

        async Task<Guid?> RuleReminderId()
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            return await db.UserNotifications.Where(n => n.TargetId == ruleId && n.RecipientUserId == f.OwnerUserId)
                .Select(n => (Guid?)n.Id).SingleAsync(ct);
        }
    }
}
