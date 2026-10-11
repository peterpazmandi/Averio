using System.Data;
using Averio.Application.Categories.ManageCategories;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Households.Invitations;
using Averio.Application.Transfers;
using Averio.Domain.Accounts;
using Averio.Domain.Categories;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class InvitationReviewVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private sealed record Home(Guid User, string Email, Household Household, Guid Member, Guid? Successor);
    private sealed record Ledger(Account Source, Account Destination, Transfer? Transfer, Category Category);

    public static async Task RunAsync(IServiceProvider provider, MutableTimeProvider clock)
    {
        await VerifyLifecycleAsync(provider, clock);
        foreach (var operation in new[] { "create", "update", "void" })
            await TransferBeforeAcceptanceAsync(provider, clock, operation);
        foreach (var solo in new[] { false, true })
        {
            foreach (var operation in new[] { "create", "update", "void" })
                await DepartureBeforeWriteAsync(provider, clock, solo, "transfer", operation);
            foreach (var operation in new[] { "create", "update", "archive", "restore" })
                await DepartureBeforeWriteAsync(provider, clock, solo, "category", operation);
        }
        Console.WriteLine("PASS invitation review regressions: successor resend, consumed token/ID replay, all transfer cutover commit orders and departed category writers including archived households");
    }

    private static async Task<Home> SeedHomeAsync(IServiceProvider provider, TimeProvider clock, bool solo = true)
    {
        var user = Guid.NewGuid(); var email = user + "@review.test"; var now = clock.GetUtcNow();
        var household = Household.CreateForUser(user, now);
        household.CompleteOnboarding("Review home " + user, "HU", CurrencyCode.Eur, now);
        household.CompleteFinancialSetup(now);
        Guid? successor = null;
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
        void AddUser(Guid id, string address) => db.Users.Add(new ApplicationUser {
            Id = id, Email = address, NormalizedEmail = address.ToUpperInvariant(), UserName = address,
            NormalizedUserName = address.ToUpperInvariant(), EmailConfirmed = true, DisplayName = "Review member",
            CreatedAtUtc = now, SecurityStamp = Guid.NewGuid().ToString() });
        AddUser(user, email);
        if (!solo)
        {
            var other = Guid.NewGuid(); AddUser(other, other + "@review.test");
            household.AddMember(other, now);
            successor = household.Members.Single(m => m.UserId == other).Id;
        }
        db.Add(household); await db.SaveChangesAsync();
        return new(user, email, household, household.Members.Single(m => m.UserId == user).Id, successor);
    }

    private static InvitationHandler Invitations(IServiceProvider sp, Guid user) =>
        ActivatorUtilities.CreateInstance<InvitationHandler>(sp, new MutableCurrentUser { UserId = user });

    private static async Task<T> RunAsync<T>(IServiceProvider provider, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = provider.CreateAsyncScope();
        return await action(scope.ServiceProvider);
    }

    private static async Task<string> InviteAsync(IServiceProvider provider, Home target, Home recipient)
    {
        await RunAsync(provider, async sp => {
            await Invitations(sp, target.User).SendAsync(target.Household.Id, new(recipient.Email), null, null, Ct);
            return true;
        });
        return provider.GetRequiredService<CapturingInvitationSender>().Tokens[recipient.Email];
    }

    private static async Task<InvitationAcceptance> ReviewAsync(IServiceProvider provider, Home user, string token)
    {
        var plan = (await RunAsync(provider, sp => Invitations(sp, user.User).PreviewAsync(token, Ct))).Departure!;
        return new(token, null, plan.Version, true, user.Successor,
            plan.Accounts.Select(a => new AccountCutoverChoice(a.Id, a.Version, a.Balance, "private")).ToArray());
    }

    private static async Task VerifyLifecycleAsync(IServiceProvider provider, MutableTimeProvider clock)
    {
        var source = await SeedHomeAsync(provider, clock, false);
        var destination = await SeedHomeAsync(provider, clock);
        var recipient = await SeedHomeAsync(provider, clock);
        var oldToken = await InviteAsync(provider, source, recipient);
        var invitation = (await RunAsync(provider, sp => Invitations(sp, source.User).ListAsync(source.Household.Id, Ct)))
            .Single(x => x.Email == recipient.Email);
        var departingToken = await InviteAsync(provider, destination, source);
        var acceptance = await ReviewAsync(provider, source, departingToken);
        await RunAsync(provider, sp => Invitations(sp, source.User).AcceptAsync(acceptance, Ct));
        var successorUser = source.Household.Members.Single(m => m.Id == source.Successor).UserId;
        clock.Advance(TimeSpan.FromMinutes(2));
        await RunAsync(provider, async sp => {
            await Invitations(sp, successorUser).SendAsync(source.Household.Id, null, invitation.Id, invitation.Version, Ct);
            return true;
        });
        var token = provider.GetRequiredService<CapturingInvitationSender>().Tokens[recipient.Email];
        Check(token != oldToken, "Successor resend rotates the credential");
        await ThrowsAsync<ConflictException>(() => RunAsync(provider, sp => Invitations(sp, recipient.User).PreviewAsync(oldToken, Ct)));
        var pending = await RunAsync(provider, sp => Invitations(sp, recipient.User).PendingAsync(Ct));
        Check(pending.Any(x => x.Id == invitation.Id), "Successor's invitation is discoverable");
        acceptance = await ReviewAsync(provider, recipient, token);
        await RunAsync(provider, sp => Invitations(sp, recipient.User).AcceptAsync(acceptance, Ct));
        Check((await RunAsync(provider, sp => Invitations(sp, recipient.User).PreviewAsync(token, Ct))).AlreadyJoined,
            "Accepted link remains idempotent in its destination");
        Check(await RunAsync(provider, sp => Invitations(sp, recipient.User).AcceptAsync(acceptance, Ct)) == source.Household.Id,
            "Accepted request is idempotent in its destination");
        var nextToken = await InviteAsync(provider, destination, recipient);
        var nextAcceptance = await ReviewAsync(provider, recipient, nextToken);
        await RunAsync(provider, sp => Invitations(sp, recipient.User).AcceptAsync(nextAcceptance, Ct));
        var beforeHistory = await RunAsync(provider, sp => sp.GetRequiredService<AverioDbContext>()
            .Set<HouseholdHistory>().CountAsync(h => h.UserId == recipient.User));
        var currentVersion = await RunAsync(provider, sp => sp.GetRequiredService<AverioDbContext>()
            .Households.Where(h => h.Id == destination.Household.Id).Select(h => h.Version).SingleAsync());
        foreach (var byId in new[] { false, true })
        {
            var consumed = nextAcceptance with { Token = byId ? null : token,
                InvitationId = byId ? invitation.Id : null, ExpectedSourceVersion = currentVersion };
            var previewError = await ThrowsAsync<ConflictException>(() => RunAsync(provider,
                sp => Invitations(sp, recipient.User).PreviewAsync(consumed.Token, consumed.InvitationId, Ct)));
            var acceptError = await ThrowsAsync<ConflictException>(() => RunAsync(provider,
                sp => Invitations(sp, recipient.User).AcceptAsync(consumed, Ct)));
            Check(previewError.Code == "invitation_invalid" && acceptError.Code == "invitation_invalid",
                "Consumed links return the localized business conflict, before departure planning/writes");
        }
        await RunAsync(provider, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            Check((await db.HouseholdMembers.SingleAsync(m => m.UserId == recipient.User)).HouseholdId == destination.Household.Id,
                "Consumed-link replay retains the current membership");
            Check(await db.Set<HouseholdHistory>().CountAsync(h => h.UserId == recipient.User) == beforeHistory,
                "Consumed-link replay creates no extra snapshots");
            Check((await db.Set<HouseholdInvitation>().SingleAsync(i => i.Id == invitation.Id)).InviterUserId == successorUser,
                "The resent token records its effective owner authority");
            return true;
        });
    }

    private static async Task<Ledger> SeedLedgerAsync(IServiceProvider provider, Home home, TimeProvider clock,
        bool personal, string operation)
    {
        await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
        var now = clock.GetUtcNow(); var date = DateOnly.FromDateTime(now.UtcDateTime);
        var source = Account.Create(home.Household.Id, [home.Member], "Review source", AccountType.Checking,
            personal ? AccountScope.Personal : AccountScope.Household,
            personal ? AccountVisibility.Private : AccountVisibility.Household, !personal, CurrencyCode.Eur, 100, date, now);
        var destination = Account.Create(home.Household.Id, [], "Review destination", AccountType.Savings,
            AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 0, date, now);
        var icon = await db.Icons.Select(i => i.Value).FirstAsync();
        var category = Category.Create(home.Household.Id, "Review category", CategoryKind.Expense, icon, "#10B981", null, now);
        if (operation == "restore") category.SetArchived(true, now);
        Transfer? transfer = operation == "create" ? null : Transfer.Create(home.Household.Id, source.Id, destination.Id,
            home.Member, CurrencyCode.Eur, CurrencyCode.Eur, 10, 10, date, "Original", now);
        db.AddRange(source, destination, category); if (transfer is not null) db.Add(transfer);
        await db.SaveChangesAsync(); return new(source, destination, transfer, category);
    }

    private static Task WriteAsync(IServiceProvider sp, Home home, Ledger ledger, string feature, string operation, TimeProvider clock)
    {
        if (feature == "transfer")
        {
            var handler = ActivatorUtilities.CreateInstance<TransferHandler>(sp, new MutableCurrentUser { UserId = home.User });
            var input = new TransferInput(20, 20, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), "Changed");
            return operation switch {
                "create" => handler.CreateAsync(home.Household.Id, ledger.Source.Id, ledger.Destination.Id, input, Ct),
                "update" => handler.UpdateAsync(home.Household.Id, ledger.Transfer!.Id, input, ledger.Transfer.UpdatedAt, Ct),
                _ => handler.VoidAsync(home.Household.Id, ledger.Transfer!.Id, ledger.Transfer.UpdatedAt, Ct)
            };
        }
        var categories = ActivatorUtilities.CreateInstance<CategoryManagementHandler>(sp, new MutableCurrentUser { UserId = home.User });
        var category = new CategoryInput("Changed category", CategoryKind.Expense, ledger.Category.Icon, "#10B981", null);
        return operation switch {
            "create" => categories.CreateAsync(home.Household.Id, category, Ct),
            "update" => categories.UpdateAsync(home.Household.Id, ledger.Category.Id, category, ledger.Category.UpdatedAt, Ct),
            _ => categories.SetArchivedAsync(home.Household.Id, ledger.Category.Id, operation != "restore", ledger.Category.UpdatedAt, Ct)
        };
    }

    private static async Task TransferBeforeAcceptanceAsync(IServiceProvider provider, MutableTimeProvider clock, string operation)
    {
        var home = await SeedHomeAsync(provider, clock); var target = await SeedHomeAsync(provider, clock);
        var ledger = await SeedLedgerAsync(provider, home, clock, true, operation);
        var token = await InviteAsync(provider, target, home); var reviewed = await ReviewAsync(provider, home, token);
        var interceptor = provider.GetRequiredService<TransferReadInterceptor>();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        interceptor.AfterAccountLock = async () => { locked.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(15)); };
        await using var writer = provider.CreateAsyncScope(); await using var joining = provider.CreateAsyncScope();
        var writing = WriteAsync(writer.ServiceProvider, home, ledger, "transfer", operation, clock);
        Task<Guid>? accepting = null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            accepting = Invitations(joining.ServiceProvider, home.User).AcceptAsync(reviewed, Ct);
            await WaitForHouseholdLockAsync(provider, joining.ServiceProvider.GetRequiredService<AverioDbContext>(), accepting);
        }
        finally { interceptor.AfterAccountLock = null; release.TrySetResult(); }
        await writing.WaitAsync(TimeSpan.FromSeconds(10));
        await ThrowsAsync<ConflictException>(() => accepting!.WaitAsync(TimeSpan.FromSeconds(10)));
        var fresh = await ReviewAsync(provider, home, token);
        var balance = operation == "void" ? 100m : 80m;
        Check(fresh.Accounts.Single().OpeningBalance == balance, "Every transfer mutation requires a fresh cutover balance");
        await RunAsync(provider, sp => Invitations(sp, home.User).AcceptAsync(fresh, Ct));
        await RunAsync(provider, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            Check((await db.Accounts.SingleAsync(a => a.HouseholdId == target.Household.Id)).BalanceAnchorAmount == balance,
                "Copied opening balance includes the committed transfer mutation");
            var history = sp.GetRequiredService<IHouseholdHistoryRepository>();
            var snapshot = await history.GetAsync(home.User, (await history.ListAsync(home.User, Ct)).Single().Id, Ct);
            Check(decimal.Parse(snapshot!.Accounts.Single(a => a.Id == ledger.Source.Id).Balance, System.Globalization.CultureInfo.InvariantCulture) == balance, "Frozen history includes the committed balance");
            Check(snapshot.Transfers.Single().IsVoided == (operation == "void"), "Frozen transfer status includes the committed mutation");
            Check(!await db.Transfers.AnyAsync(t => t.HouseholdId == target.Household.Id), "No old transfers enter the destination");
            return true;
        });
    }

    private static async Task DepartureBeforeWriteAsync(IServiceProvider provider, MutableTimeProvider clock,
        bool solo, string feature, string operation)
    {
        var home = await SeedHomeAsync(provider, clock, solo); var target = await SeedHomeAsync(provider, clock);
        var ledger = await SeedLedgerAsync(provider, home, clock, false, operation);
        var token = await InviteAsync(provider, target, home); var reviewed = await ReviewAsync(provider, home, token);
        var interceptor = provider.GetRequiredService<TransferReadInterceptor>();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        interceptor.HouseholdToPause = home.Household.Id;
        interceptor.AfterHouseholdLock = async () => { locked.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(15)); };
        await using var joining = provider.CreateAsyncScope(); await using var writer = provider.CreateAsyncScope();
        var accepting = Invitations(joining.ServiceProvider, home.User).AcceptAsync(reviewed, Ct);
        Task? writing = null;
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            writing = WriteAsync(writer.ServiceProvider, home, ledger, feature, operation, clock);
            await WaitForHouseholdLockAsync(provider, writer.ServiceProvider.GetRequiredService<AverioDbContext>(), writing);
        }
        finally { interceptor.AfterHouseholdLock = null; interceptor.HouseholdToPause = null; release.TrySetResult(); }
        await accepting.WaitAsync(TimeSpan.FromSeconds(10));
        await ThrowsAsync<NotFoundException>(() => writing!.WaitAsync(TimeSpan.FromSeconds(10)));
        await RunAsync(provider, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            var stored = await db.Transfers.Where(t => t.HouseholdId == home.Household.Id).ToListAsync();
            Check(stored.Count == (ledger.Transfer is null ? 0 : 1) && stored.All(t => t.SentAmount == 10 && t.VoidedAt is null),
                "Rejected departed transfer writers leave the original ledger unchanged");
            var categories = await db.Categories.Where(c => c.HouseholdId == home.Household.Id).ToListAsync();
            Check(categories.Count == 1 && categories[0].Name == "Review category" && categories[0].IsArchived == (operation == "restore"),
                "Rejected departed category writers leave the directory unchanged");
            Check((await db.Households.SingleAsync(h => h.Id == home.Household.Id)).ArchivedAtUtc.HasValue == solo,
                "Both active and archived source households retain their data");
            return true;
        });
    }

    private static async Task WaitForHouseholdLockAsync(IServiceProvider provider, AverioDbContext waitingDb, Task pending)
    {
        await using var observer = provider.CreateAsyncScope(); var db = observer.ServiceProvider.GetRequiredService<AverioDbContext>();
        var connection = (NpgsqlConnection)waitingDb.Database.GetDbConnection();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            Check(!pending.IsCompleted, "The real handler must wait before reading or mutating finances");
            if (connection.State == ConnectionState.Open && await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {connection.ProcessID} AND wait_event_type = 'Lock' AND query ILIKE '%FROM households%') AS \"Value\"").SingleAsync())
                return;
            await Task.Delay(20);
        }
        throw new InvalidOperationException("Handler did not wait on the household lock.");
    }

    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T error) { return error; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
}
