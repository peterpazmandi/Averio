using Averio.Application.Notifications;
using Averio.Domain.Notifications;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

public sealed class NotificationInbox(AverioDbContext db, TimeProvider clock) : INotificationInbox
{
    private const string Submission = "ReimbursementSubmissionDue";
    private const string Overdue = "ReimbursementPaymentOverdue";
    private const string Confirmation = "RecurringConfirmationDue";
    private const string Review = "RecurringNeedsReview";
    private static bool IsClaim(string type) => type is Submission or Overdue;
    private sealed record Scope(Guid HouseholdId, Guid MemberId, HashSet<Guid> Claims, HashSet<Guid> Rules);

    private async Task<List<Scope>> ScopesAsync(Guid userId, CancellationToken ct)
    {
        var memberships = await db.HouseholdMembers.AsNoTracking().Where(m => m.UserId == userId && m.LeftAtUtc == null)
            .Select(m => new { m.HouseholdId, MemberId = m.Id }).ToArrayAsync(ct);
        var result = new List<Scope>(memberships.Length);
        foreach (var membership in memberships)
        {
            var claims = await ReimbursementRepository.Visible(db, membership.HouseholdId, membership.MemberId)
                .AsNoTracking().Select(c => c.Id).ToArrayAsync(ct);
            var rules = await db.Set<RecurringRule>().AsNoTracking()
                .Where(r => r.HouseholdId == membership.HouseholdId && r.HouseholdMemberId == membership.MemberId)
                .Select(r => r.Id).ToArrayAsync(ct);
            result.Add(new(membership.HouseholdId, membership.MemberId, claims.ToHashSet(), rules.ToHashSet()));
        }
        return result;
    }

    private static bool Allowed(UserNotification n, IReadOnlyList<Scope> scopes) => scopes.Any(s =>
        s.HouseholdId == n.HouseholdId && s.MemberId == n.HouseholdMemberId &&
        (IsClaim(n.Type) ? s.Claims.Contains(n.TargetId) : n.Type is Confirmation or Review && s.Rules.Contains(n.TargetId)));

    // Derived reminders are materialized on access and at night. The lock serializes two tabs
    // (and the nightly worker) for the same recipient; the unique index is a second guard.
    private async Task ReconcileAsync(Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({userId.ToString()}, 0))", ct);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var scopes = await ScopesAsync(userId, ct);
        var due = new List<UserNotification>();
        foreach (var scope in scopes)
        {
            var claims = await ReimbursementRepository.Visible(db, scope.HouseholdId, scope.MemberId)
                .AsNoTracking().Include(c => c.Payments).Where(c => c.Status != ReimbursementStatus.Cancelled
                    && db.Transactions.Any(t => t.Id == c.TransactionId && t.Status == TransactionStatus.Posted
                        && t.Kind == TransactionKind.Expense))
                .ToArrayAsync(ct);
            foreach (var claim in claims)
            {
                if (claim.Status == ReimbursementStatus.NeedsSubmission && claim.SubmissionReminderOn is DateOnly reminder && reminder <= today)
                    due.Add(UserNotification.Create(userId, scope.HouseholdId, scope.MemberId, Submission, claim.Id,
                        reminder.ToString("yyyy-MM-dd"), now));
                if (claim.Status is (ReimbursementStatus.Submitted or ReimbursementStatus.PartiallyReceived)
                    && claim.OutstandingAmount > 0 && claim.ExpectedOn is DateOnly expected && expected < today)
                    due.Add(UserNotification.Create(userId, scope.HouseholdId, scope.MemberId, Overdue, claim.Id,
                        expected.ToString("yyyy-MM-dd"), now));
            }
            var rules = await db.Set<RecurringRule>().AsNoTracking()
                .Where(r => r.HouseholdId == scope.HouseholdId && r.HouseholdMemberId == scope.MemberId
                    && !r.IsStopped && !r.IsPaused).ToArrayAsync(ct);
            foreach (var rule in rules)
            {
                if (rule.IsBlocked)
                    due.Add(UserNotification.Create(userId, scope.HouseholdId, scope.MemberId, Review, rule.Id,
                        rule.Version.ToString("N"), now));
                else if (!rule.Automatic && rule.NextDueOn is DateOnly next && next <= today)
                    due.Add(UserNotification.Create(userId, scope.HouseholdId, scope.MemberId, Confirmation, rule.Id,
                        next.ToString("yyyy-MM-dd"), now));
            }
        }
        var existing = await db.UserNotifications.Where(n => n.RecipientUserId == userId && n.ResolvedAt == null).ToArrayAsync(ct);
        var keys = due.Select(n => (n.Type, n.TargetId, n.OccurrenceKey)).ToHashSet();
        foreach (var row in existing.Where(n => !keys.Contains((n.Type, n.TargetId, n.OccurrenceKey)))) row.Resolve(now);
        var known = await db.UserNotifications.AsNoTracking().Where(n => n.RecipientUserId == userId)
            .Select(n => new { n.Type, n.TargetId, n.OccurrenceKey }).ToArrayAsync(ct);
        var knownKeys = known.Select(n => (n.Type, n.TargetId, n.OccurrenceKey)).ToHashSet();
        db.UserNotifications.AddRange(due.Where(n => !knownKeys.Contains((n.Type, n.TargetId, n.OccurrenceKey))));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<List<UserNotification>> AuthorizedAsync(Guid userId, CancellationToken ct)
    {
        await ReconcileAsync(userId, ct);
        var scopes = await ScopesAsync(userId, ct);
        var rows = await db.UserNotifications.Where(n => n.RecipientUserId == userId && n.ResolvedAt == null && n.DismissedAt == null)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).ToListAsync(ct);
        return rows.Where(n => Allowed(n, scopes)).ToList();
    }

    public async Task<IReadOnlyList<NotificationItem>> ListAsync(Guid userId, Guid householdId, bool unreadOnly, CancellationToken ct)
        => (await AuthorizedAsync(userId, ct)).Where(n => n.HouseholdId == householdId && (!unreadOnly || n.ReadAt == null)).Take(100)
            .Select(n => new NotificationItem(n.Id, n.HouseholdId, n.Type, n.TargetId, n.CreatedAt, n.ReadAt)).ToArray();

    public async Task<int> UnreadCountAsync(Guid userId, Guid householdId, CancellationToken ct)
        => (await AuthorizedAsync(userId, ct)).Count(n => n.HouseholdId == householdId && n.ReadAt == null);

    public async Task<bool> MarkReadAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = (await AuthorizedAsync(userId, ct)).SingleOrDefault(n => n.Id == id);
        if (row is null) return false;
        row.MarkRead(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, Guid householdId, CancellationToken ct)
    {
        var rows = (await AuthorizedAsync(userId, ct)).Where(n => n.HouseholdId == householdId && n.ReadAt == null).ToArray();
        foreach (var row in rows) row.MarkRead(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return rows.Length;
    }

    public async Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var row = (await AuthorizedAsync(userId, ct)).SingleOrDefault(n => n.Id == id);
        if (row is null) return false;
        row.Dismiss(clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task ReconcileAllAsync(CancellationToken ct)
    {
        // Keyset traversal avoids loading all users in memory during nightly maintenance.
        var after = Guid.Empty;
        while (true)
        {
            var users = await db.HouseholdMembers.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.LeftAtUtc == null && m.UserId.CompareTo(after) > 0)
                .OrderBy(m => m.UserId).Select(m => m.UserId).Distinct().Take(100).ToArrayAsync(ct);
            if (users.Length == 0) break;
            foreach (var user in users)
            {
                await ReconcileAsync(user, ct);
                db.ChangeTracker.Clear();
            }
            after = users[^1];
        }
    }
}
