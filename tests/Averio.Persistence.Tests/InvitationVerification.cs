using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Households.Invitations;
using Averio.Application.Accounts.GetAccounts;
using Averio.Domain.Households;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal sealed class CapturingInvitationSender : IInvitationEmailSender
{
    public readonly Dictionary<string, string> Tokens = new(StringComparer.OrdinalIgnoreCase);
    public bool Succeed = true;
    public Task<bool> SendAsync(string email, string token, CancellationToken ct) { Tokens[email] = token; return Task.FromResult(Succeed); }
}

internal static class InvitationVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    public static async Task RunAsync(IServiceProvider provider, Fixture f, MutableTimeProvider clock)
    {
        var sender = provider.GetRequiredService<CapturingInvitationSender>();
        async Task<T> Run<T>(Guid user, Func<InvitationHandler, Task<T>> action)
        {
            await using var scope = provider.CreateAsyncScope();
            var handler = ActivatorUtilities.CreateInstance<InvitationHandler>(scope.ServiceProvider, new MutableCurrentUser { UserId = user });
            return await action(handler);
        }
        async Task Send(Guid user, string email) => await Run(user, async h => { await h.SendAsync(f.HouseholdId, new(email), null, null, Ct); return true; });
        Task<IReadOnlyList<InvitationView>> List() => Run(f.OwnerUserId, h => h.ListAsync(f.HouseholdId, Ct));
        async Task<(Guid User, Guid Household, Guid Member, string Email)> NewSolo(bool confirmed = true)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var id = Guid.NewGuid(); var email = id + "@invitation.test";
            db.Users.Add(new ApplicationUser { Id = id, Email = email, NormalizedEmail = email.ToUpperInvariant(),
                UserName = email, NormalizedUserName = email.ToUpperInvariant(), EmailConfirmed = confirmed,
                CreatedAtUtc = clock.GetUtcNow(), SecurityStamp = Guid.NewGuid().ToString() });
            var household = Household.CreateForUser(id, clock.GetUtcNow());
            db.Households.Add(household); await db.SaveChangesAsync();
            return (id, household.Id, household.Members.Single().Id, email);
        }
        var invitee = await NewSolo();
        await Throws<ForbiddenException>(() => Send(f.MemberUserId, invitee.Email));
        await Throws<NotFoundException>(() => Send(invitee.User, invitee.Email));
        await Throws<ForbiddenException>(() => Run(f.MemberUserId, h => h.ListAsync(f.HouseholdId, Ct)));
        sender.Succeed = false;
        await Send(f.OwnerUserId, invitee.Email);
        var row = (await List()).Single(x => x.Email == invitee.Email);
        Check(row.DeliveryStatus == "Failed", "Delivery failure must be persisted");
        await AssertLifecycleCheckRejectsAsync(provider, row.Id,
            "partial-acceptance-metadata",
            clock.GetUtcNow());
        await AssertLifecycleCheckRejectsAsync(provider, row.Id,
            "accepted-without-metadata");
        await AssertLifecycleCheckRejectsAsync(provider, row.Id,
            "revoked-without-timestamp");
        var oldToken = sender.Tokens[invitee.Email];
        await Throws<ConflictException>(() => Send(f.OwnerUserId, invitee.Email.ToUpperInvariant()));
        await Throws<ConflictException>(() => Run(f.OwnerUserId, async h => { await h.SendAsync(f.HouseholdId, null, row.Id, row.Version, Ct); return true; }));
        clock.Advance(TimeSpan.FromMinutes(2)); sender.Succeed = true;
        await Run(f.OwnerUserId, async h => { await h.SendAsync(f.HouseholdId, null, row.Id, row.Version, Ct); return true; });
        var token = sender.Tokens[invitee.Email];
        Check(token != oldToken, "Resend must rotate token");
        await Throws<ConflictException>(() => Run(invitee.User, h => h.PreviewAsync(oldToken, Ct)));
        await Throws<ConflictException>(() => Run(f.OwnerUserId, h => h.PreviewAsync(token, Ct)));
        var preview = await Run(invitee.User, h => h.PreviewAsync(token, Ct));
        Check(preview.CanJoin && !preview.AlreadyJoined, "New invitee can preview before onboarding");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(await db.Households.AnyAsync(x => x.Id == invitee.Household), "Preview never consumes membership");
            Check(!await db.Set<HouseholdInvitation>().AnyAsync(x => x.TokenHash == token), "Raw tokens must not be stored");
        }
        var joined = await Task.WhenAll(Run(invitee.User, h => h.AcceptAsync(token, Ct)), Run(invitee.User, h => h.AcceptAsync(token, Ct)));
        Check(joined.All(x => x == f.HouseholdId), "Concurrent acceptance must be idempotent");
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var member = await db.HouseholdMembers.SingleAsync(x => x.UserId == invitee.User);
            Check(member.HouseholdId == f.HouseholdId && member.Role == HouseholdRole.Member, "Invite grants member only");
            var accepted = await db.Set<HouseholdInvitation>().SingleAsync(x => x.Email == invitee.Email);
            Check(accepted.State == "Accepted" && accepted.AcceptedAt is not null
                && accepted.AcceptedByUserId == invitee.User && accepted.SourceHouseholdId == invitee.Household
                && accepted.RevokedAt is null, "Accepted invitation lifecycle metadata persists consistently");
            Check(!await db.Households.AnyAsync(x => x.Id == invitee.Household), "Only empty source is removed");
            var accounts = ActivatorUtilities.CreateInstance<GetAccountsHandler>(scope.ServiceProvider, new MutableCurrentUser { UserId = invitee.User });
            var visible = await accounts.HandleAsync(f.HouseholdId, false, Ct);
            Check(visible.Any(x => x.Id == f.HouseholdAccountId), "Joined member sees shared accounts");
            var privateIds = await db.Accounts.Where(x => x.Visibility == Averio.Domain.Accounts.AccountVisibility.Private).Select(x => x.Id).ToArrayAsync();
            Check(!visible.Any(x => privateIds.Contains(x.Id)), "Joining never grants private-account access");
        }
        var unverified = await NewSolo(false);
        await Send(f.OwnerUserId, unverified.Email);
        await Throws<EmailNotVerifiedException>(() => Run(unverified.User, h => h.AcceptAsync(sender.Tokens[unverified.Email], Ct)));
        var revoked = await NewSolo(); await Send(f.OwnerUserId, revoked.Email);
        var revokedRow = (await List()).Single(x => x.Email == revoked.Email);
        await Run(f.OwnerUserId, async h => { await h.RevokeAsync(f.HouseholdId, revokedRow.Id, revokedRow.Version, Ct); return true; });
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var stored = await db.Set<HouseholdInvitation>().SingleAsync(x => x.Id == revokedRow.Id);
            Check(stored.State == "Revoked" && stored.RevokedAt is not null
                && stored.AcceptedAt is null && stored.AcceptedByUserId is null && stored.SourceHouseholdId is null,
                "Revoked invitation lifecycle metadata persists consistently");
        }
        await Throws<ConflictException>(() => Run(revoked.User, h => h.AcceptAsync(sender.Tokens[revoked.Email], Ct)));

        // A member-only dependency must block joining, including an insert committing
        // while acceptance is waiting for its member lock (pins use ON DELETE CASCADE).
        var pinned = await NewSolo(); await Send(f.OwnerUserId, pinned.Email);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var category = await db.Categories.Where(x => x.IsBase).Select(x => x.Id).FirstAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO category_pins (household_member_id, category_id, sort_order, created_at) VALUES ({pinned.Member}, {category}, 0, {clock.GetUtcNow()})");
            var accepting = Run(pinned.User, h => h.AcceptAsync(sender.Tokens[pinned.Email], Ct));
            await tx.CommitAsync();
            await Throws<ConflictException>(() => accepting);
            Check(await db.Households.AnyAsync(x => x.Id == pinned.Household), "Concurrent pin must preserve source household");
        }
        var expired = await NewSolo(); await Send(f.OwnerUserId, expired.Email);
        clock.Advance(TimeSpan.FromDays(8));
        await Throws<ConflictException>(() => Run(expired.User, h => h.AcceptAsync(sender.Tokens[expired.Email], Ct)));
        Check((await List()).Single(x => x.Email == expired.Email).State == "Expired", "Expiry is projected");
        Console.WriteLine("PASS invitation delivery, rotation, permissions, verified email, atomic joining, privacy, concurrent acceptance and dependency preservation");
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task AssertLifecycleCheckRejectsAsync(
        IServiceProvider provider,
        Guid id,
        string invalidWrite,
        DateTimeOffset? timestamp = null)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
        try
        {
            switch (invalidWrite)
            {
                case "partial-acceptance-metadata":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE household_invitations SET \"AcceptedAt\" = {timestamp} WHERE \"Id\" = {id}",
                        CancellationToken.None);
                    break;
                case "accepted-without-metadata":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE household_invitations SET \"State\" = 'Accepted' WHERE \"Id\" = {id}",
                        CancellationToken.None);
                    break;
                case "revoked-without-timestamp":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE household_invitations SET \"State\" = 'Revoked' WHERE \"Id\" = {id}",
                        CancellationToken.None);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(invalidWrite));
            }
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.CheckViolation
            && exception.ConstraintName == "ck_invitation_lifecycle_metadata")
        {
            return;
        }

        throw new InvalidOperationException("Expected ck_invitation_lifecycle_metadata to reject the write.");
    }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
}
