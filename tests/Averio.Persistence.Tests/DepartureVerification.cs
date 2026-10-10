using System.Globalization;
using Averio.Application.Accounts.GetAccounts;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Households.Invitations;
using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Statistics.GetCashFlow;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Categories;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Email;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class DepartureVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    public static async Task RunAsync(IServiceProvider provider, Fixture target, MutableTimeProvider clock)
    {
        var now = clock.GetUtcNow(); var today = DateOnly.FromDateTime(now.UtcDateTime);
        var sender = provider.GetRequiredService<CapturingInvitationSender>();
        async Task<T> Run<T>(Guid user, Func<IServiceProvider, Task<T>> action)
        {
            await using var scope = provider.CreateAsyncScope();
            return await action(scope.ServiceProvider);
        }
        InvitationHandler Handler(IServiceProvider sp, Guid id) => ActivatorUtilities.CreateInstance<InvitationHandler>(sp, new MutableCurrentUser { UserId = id });
        var leaving = Guid.NewGuid(); var remaining = Guid.NewGuid();
        var source = Household.CreateForUser(leaving, now);
        source.CompleteOnboarding("Previous home", "HU", CurrencyCode.From("EUR"), now);
        source.CompleteFinancialSetup(now); source.AddMember(remaining, now);
        var oldMember = source.Members.Single(x => x.UserId == leaving).Id;
        var successor = source.Members.Single(x => x.UserId == remaining).Id;
        var email = leaving + "@departure.test";
        var personal = Account.Create(source.Id, [oldMember], "My shared account", AccountType.Checking, AccountScope.Personal,
            AccountVisibility.Household, true, CurrencyCode.From("EUR"), 200.0001m, today, now);
        var privateAccount = Account.Create(source.Id, [oldMember], "My private account", AccountType.Savings, AccountScope.Personal,
            AccountVisibility.Private, false, CurrencyCode.From("HUF"), 899999999999999.9999m, today, now);
        var householdAccount = Account.Create(source.Id, [oldMember], "Household money", AccountType.Cash, AccountScope.Household,
            AccountVisibility.Household, true, CurrencyCode.From("EUR"), 0, today, now);
        var joint = Account.Create(source.Id, [oldMember, successor], "Joint personal", AccountType.Checking, AccountScope.Personal,
            AccountVisibility.Household, true, CurrencyCode.From("EUR"), 0, today, now);
        var hidden = Account.Create(source.Id, [successor], "Other member private", AccountType.Cash, AccountScope.Personal,
            AccountVisibility.Private, false, CurrencyCode.From("EUR"), 999, today, now);
        Guid category;
        var customCategory = Category.Create(source.Id, "income-salary", CategoryKind.Both, "cash", "#10B981", null, now);
        await Run(leaving, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            foreach (var user in new[] { leaving, remaining })
            {
                var address = user == leaving ? email : user + "@departure.test";
                db.Users.Add(new ApplicationUser { Id = user, Email = address, NormalizedEmail = address.ToUpperInvariant(),
                    UserName = address, NormalizedUserName = address.ToUpperInvariant(), DisplayName = user == leaving ? "Former person" : "Successor",
                    EmailConfirmed = true, CreatedAtUtc = now, SecurityStamp = Guid.NewGuid().ToString() });
            }
            db.Add(source); db.Add(customCategory); db.Accounts.AddRange(personal, privateAccount, householdAccount, joint, hidden);
            await db.SaveChangesAsync(); return true;
        });
        category = await Run(leaving, async sp => await sp.GetRequiredService<AverioDbContext>().Categories.Where(x => x.IsBase && x.SystemKey == "income-salary").Select(x => x.Id).SingleAsync());
        var income = Transaction.Create(source.Id, personal.Id, oldMember, TransactionKind.Income, TransactionVisibility.Household,
            personal.Currency, "Income", null, today, [new("Salary", category, "income-salary", "cash", 100.1234m)], [], now);
        var expense = Transaction.Create(source.Id, personal.Id, oldMember, TransactionKind.Expense, TransactionVisibility.Private,
            personal.Currency, "Private purchase", null, today, [new("Gift", customCategory.Id, "income-salary", "cash", 50.1111m)], [], now);
        var transfer = Transfer.Create(source.Id, personal.Id, householdAccount.Id, oldMember, personal.Currency, householdAccount.Currency,
            37.0001m, 37.0001m, today, "Historical transfer", now);
        var hiddenTransfer = Transfer.Create(source.Id, householdAccount.Id, hidden.Id, successor, householdAccount.Currency, hidden.Currency,
            5m, 5m, today, "SECRET hidden-leg note", now);
        await Run(leaving, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>(); await using var tx = await db.Database.BeginTransactionAsync();
            db.AddRange(income, expense, transfer, hiddenTransfer); await db.SaveChangesAsync();
            await sp.GetRequiredService<TransactionAggregateWriter>().ApplyAsync(income, 1, Ct);
            await tx.CommitAsync(); return true;
        });
        async Task<CashFlowResult> CashFlow() => await Run(remaining, sp => ActivatorUtilities.CreateInstance<GetCashFlowHandler>(sp,
            new MutableCurrentUser { UserId = remaining }).HandleAsync(source.Id, today, today, TransactionAggregatePeriod.Day, Ct));
        var before = await CashFlow();
        await Run(target.OwnerUserId, async sp => { await Handler(sp, target.OwnerUserId).SendAsync(target.HouseholdId, new(email), null, null, Ct); return true; });
        var token = sender.Tokens[email];
        var discovered = (await Run(leaving, sp => Handler(sp, leaving).PendingAsync(Ct))).Single();
        var withoutTabToken = await Run(leaving, sp => Handler(sp, leaving).PreviewAsync(null, discovered.Id, Ct));
        Check(withoutTabToken.HouseholdName == discovered.HouseholdName && withoutTabToken.Departure?.Accounts.Count == 2,
            "Verified invitee can resume a pending invitation without the original browser token");
        var preview = await Run(leaving, sp => Handler(sp, leaving).PreviewAsync(token, Ct));
        var plan = preview.Departure!;
        Check(plan.RequiresSuccessor && !plan.WillArchive && plan.Accounts.Count == 2 && plan.AccountsStaying == 2,
            "Only sole-holder active personal accounts are eligible; private accounts of others are not revealed");
        Check(plan.Accounts.Single(x => x.Id == personal.Id).Balance == 213.0123m, "Cutover includes income, private expense and transfer at exact precision");
        var choices = plan.Accounts.Select(x => new AccountCutoverChoice(x.Id, x.Version, x.Balance, x.Id == privateAccount.Id ? "private" : "shared")).ToArray();
        var input = new InvitationAcceptance(token, null, plan.Version, true, successor, choices);
        await Throws<ConflictException>(() => Run(leaving, sp => Handler(sp, leaving).AcceptAsync(input with { SuccessorMemberId = Guid.NewGuid() }, Ct)));
        await Throws<ConflictException>(() => Run(leaving, sp => Handler(sp, leaving).AcceptAsync(input with { Confirmed = false }, Ct)));
        await Throws<ConflictException>(() => Run(leaving, sp => Handler(sp, leaving).AcceptAsync(input with { Accounts = [choices[0]] }, Ct)));
        await Run(leaving, async sp => { await Handler(sp, leaving).AcceptAsync(input, Ct); return true; });
        await Run(leaving, async sp => { await Handler(sp, leaving).AcceptAsync(input, Ct); return true; });
        var current = await Run(leaving, sp => ActivatorUtilities.CreateInstance<GetCurrentUserHandler>(sp,
            new MutableCurrentUser { UserId = leaving }).HandleAsync(Ct));
        Check(current.HouseholdId == target.HouseholdId && current.HouseholdRole == HouseholdRole.Member
            && !current.OnboardingRequired, "Current user immediately resolves the destination household despite retained source membership");
        await Run(leaving, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            Check(await db.HouseholdMembers.IgnoreQueryFilters().CountAsync(x => x.UserId == leaving) == 2
                && await db.HouseholdMembers.CountAsync(x => x.UserId == leaving && x.HouseholdId == target.HouseholdId) == 1,
                "Retained historical membership never becomes a second active household");
            return true;
        });
        var after = await CashFlow();
        Check(before.Periods.SequenceEqual(after.Periods), "Departure preserves source cash flow");
        await Run(leaving, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            Check(await db.Households.AnyAsync(x => x.Id == source.Id && x.ArchivedAtUtc == null), "Household remains active for remaining members");
            Check(await db.HouseholdMembers.AnyAsync(x => x.Id == successor && x.Role == HouseholdRole.Owner), "Owner transferred explicitly");
            Check(await db.HouseholdMembers.IgnoreQueryFilters().AnyAsync(x => x.Id == oldMember && x.LeftAtUtc != null), "Former membership retained for attribution");
            Check(!await db.HouseholdMembers.AnyAsync(x => x.Id == oldMember), "Former membership excluded from active authorization");
            var copied = await db.Accounts.Where(x => x.HouseholdId == target.HouseholdId && (x.Name == personal.Name || x.Name == privateAccount.Name)).ToListAsync();
            Check(copied.Count == 2 && copied.Single(x => x.Name == personal.Name).BalanceAnchorAmount == 213.0123m
                && copied.Single(x => x.Name == privateAccount.Name).BalanceAnchorAmount == 899999999999999.9999m, "Copies retain exact opening balances");
            Check(!await db.Transactions.AnyAsync(x => copied.Select(a => a.Id).Contains(x.AccountId))
                && !await db.Transfers.AnyAsync(x => copied.Select(a => a.Id).Contains(x.SourceAccountId) || copied.Select(a => a.Id).Contains(x.DestinationAccountId)), "No transaction or transfer copied");
            Check(await db.Transactions.CountAsync(x => x.HouseholdId == source.Id) == 2, "Source transactions preserved");
            var original = await db.Accounts.SingleAsync(x => x.Id == personal.Id);
            Check(original.CutoverAtUtc != null && original.Status == AccountStatus.Archived, "Source personal account permanently closed");
            try { original.Restore(now); throw new InvalidOperationException("Historical account restored"); } catch (InvalidOperationException e) when (e.Message.Contains("cannot be restored")) { }
            return true;
        });
        HouseholdHistoryHandler History(IServiceProvider sp, Guid id) => ActivatorUtilities.CreateInstance<HouseholdHistoryHandler>(sp, new MutableCurrentUser { UserId = id });
        var summary = (await Run(leaving, sp => History(sp, leaving).ListAsync(Ct))).Single(x => x.HouseholdName == "Previous home");
        var snapshot = await Run(leaving, sp => History(sp, leaving).GetAsync(summary.Id, null, null, Ct));
        Check(snapshot.Transactions.Count == 2 && snapshot.Transfers.Count == 2 && snapshot.Accounts.All(x => x.Id != hidden.Id), "Snapshot retains only authorized data");
        var systemItem = snapshot.Transactions.Single(x => x.Id == income.Id).Items.Single();
        var customItem = snapshot.Transactions.Single(x => x.Id == expense.Id).Items.Single();
        Check(systemItem.CategoryName == "income-salary" && systemItem.IsSystemCategory == true
            && systemItem.Description == "Salary" && customItem.CategoryName == "income-salary"
            && customItem.IsSystemCategory == false,
            "Snapshot distinguishes system keys from identically named custom categories without translating user content");
        var legacyItem = System.Text.Json.JsonSerializer.Deserialize<HistoryItem>(
            """{"Description":"Salary","CategoryName":"income-salary","Amount":"100.1234"}""")!;
        Check(legacyItem.IsSystemCategory is null && legacyItem.CategoryName == "income-salary",
            "Older snapshot JSON remains readable without category classification or a data rewrite");
        var redacted = snapshot.Transfers.Single(x => x.Id == hiddenTransfer.Id);
        Check(redacted.Source is not null && redacted.Destination is null && redacted.Note is null,
            "Hidden transfer account, amount, currency and shared note are all redacted");
        Check(snapshot.CashFlow.Single().Income == "100.1234" && snapshot.CashFlow.Single().Expense == "0", "Private expense excluded from historical household reporting");
        Check(snapshot.Accounts.Single(x => x.Id == privateAccount.Id).Balance == "899999999999999.9999", "Historical money serialized without floating-point loss");
        var emptyRange = await Run(leaving, sp => History(sp, leaving).GetAsync(summary.Id, today.AddDays(1), today.AddDays(2), Ct));
        Check(emptyRange.Transactions.Count == 0 && emptyRange.Transfers.Count == 0 && emptyRange.CashFlow.Count == 0,
            "Date-filtered history excludes movements and cash flow outside the selected range");
        await Throws<NotFoundException>(() => Run(remaining, sp => History(sp, remaining).GetAsync(summary.Id, null, null, Ct)));
        await Throws<NotFoundException>(() => Run(leaving, sp => ActivatorUtilities.CreateInstance<GetAccountsHandler>(sp, new MutableCurrentUser { UserId = leaving }).HandleAsync(source.Id, true, Ct)));
        // A later shared entry must never appear in the former member's history.
        await Run(remaining, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            var renamedCategory = await db.Categories.SingleAsync(x => x.Id == customCategory.Id);
            renamedCategory.Update("Renamed after departure", "cash",
                "#10B981", null, now.AddMinutes(1));
            db.Add(Transaction.Create(source.Id, householdAccount.Id, successor, TransactionKind.Income, TransactionVisibility.Household,
                householdAccount.Currency, "Later", null, today, [new("Later", category, "Category", "cash", 10)], [], now.AddMinutes(1)));
            await db.SaveChangesAsync(); return true;
        });
        var frozen = await Run(leaving, sp => History(sp, leaving).GetAsync(summary.Id, null, null, Ct));
        Check(frozen.Transactions.Count == 2, "Snapshot never reveals post-departure writes");
        Check(frozen.Transactions.Single(x => x.Id == income.Id).Items.Single() == systemItem
            && frozen.Transactions.Single(x => x.Id == expense.Id).Items.Single() == customItem,
            "Post-departure category changes cannot alter frozen category references or classification");
        await VerifySoloAndRaceAsync(provider, target, clock);
        await VerifyDepartedWriterAsync(provider, clock);
        var html = InvitationEmailTemplate.Render("https://averio.test", "TOKEN", "Join <script>& home", "Body", "Review", "Privacy", "Expires", "Fallback", "Footer");
        Check(!html.Contains("<script>") && html.Contains("&lt;script&gt;") && html.Contains("#10B981")
            && html.Contains("/branding/averio-email-logo.png") && html.Contains("/invitation#TOKEN"), "Branded email escapes content and uses fragment credentials");
        var localEmail = InvitationEmailTemplate.Render("http://localhost:5173", "TOKEN", "Title", "Body", "Review",
            "Privacy", "Expires", "Fallback", "Footer");
        Check(localEmail.Contains("src=\"" + InvitationEmailTemplate.DefaultLogoUrl + "\"")
            && localEmail.Contains("href=\"http://localhost:5173/invitation#TOKEN\"")
            && !localEmail.Contains("src=\"http://localhost"),
            "Local invitation navigation keeps a separately hosted HTTPS logo for inbox image proxies");
        var customLogo = InvitationEmailTemplate.Render("https://averio.test", "TOKEN", "Title", "Body", "Review",
            "Privacy", "Expires", "Fallback", "Footer", "https://cdn.averio.test/logo.png?v=1&theme=all");
        Check(customLogo.Contains("src=\"https://cdn.averio.test/logo.png?v=1&amp;theme=all\""),
            "Configured logo URL is escaped independently of invitation navigation");
        foreach (var unsupported in new[] { "http://averio.test/logo.png", "https://localhost/logo.png",
            "https://127.0.0.1/logo.png", "https://[::1]/logo.png", "/logo.png", "https://internal.local/logo.png" })
        {
            Check(!InvitationEmailTemplate.IsSupportedLogoUrl(unsupported),
                "Reject logo addresses inbox proxies cannot safely fetch: " + unsupported);
        }
        var escapedEmail = InvitationEmailTemplate.Render("https://averio.test/", "A&\"B", "Title", "Body", "Review <now>",
            "Privacy <safe>", "Expires", "Fallback", "Footer");
        var links = System.Text.RegularExpressions.Regex.Matches(escapedEmail, "href=\"([^\"]*)\"")
            .Select(match => match.Groups[1].Value).ToArray();
        Check(links.Length == 3 && links.All(link => link == "https://averio.test/invitation#A&amp;&quot;B"),
            "Outlook, standard button and fallback link retain the same escaped invitation credential");
        Check(escapedEmail.Contains("<v:roundrect") && escapedEmail.Contains("<w:anchorlock/>")
            && escapedEmail.Contains("<!--[if !mso]><!-->") && escapedEmail.Contains("Review &lt;now&gt;")
            && escapedEmail.Contains("prefers-color-scheme:dark") && escapedEmail.Contains("[data-ogsc]")
            && escapedEmail.Contains("bgcolor=\"#FFFFFF\"") && !escapedEmail.Contains("Privacy <safe>"),
            "Email supplies Outlook button fallback, adaptive colors and escaped localized copy");
        var visibleMatch = System.Text.RegularExpressions.Regex.Match(escapedEmail,
            """<a class="av-email-fallback av-email-link"[^>]*>(.*?)</a>""",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Check(visibleMatch.Success
            && System.Net.WebUtility.HtmlDecode(visibleMatch.Groups[1].Value.Replace("<wbr>", ""))
                == "https://averio.test/invitation#A&\"B"
            && !escapedEmail.Contains('\u200B'),
            "Displayed fallback URL wraps without changing copied credentials or injecting invisible characters");
        Console.WriteLine("PASS departure account cutover, exact balances, ownership transfer, immutable privacy-filtered history, no copied movements and unchanged source cash flow");
    }
    private static async Task VerifySoloAndRaceAsync(IServiceProvider provider, Fixture target, MutableTimeProvider clock)
    {
        var now = clock.GetUtcNow(); var date = DateOnly.FromDateTime(now.UtcDateTime); var id = Guid.NewGuid(); var email = id + "@solo-departure.test";
        var h = Household.CreateForUser(id, now); h.CompleteOnboarding("Solo history", "HU", CurrencyCode.From("EUR"), now); h.CompleteFinancialSetup(now);
        var member = h.Members.Single().Id;
        var account = Account.Create(h.Id, [member], "Solo", AccountType.Cash, AccountScope.Personal, AccountVisibility.Private, false, CurrencyCode.From("EUR"), 10, date, now);
        await using (var setup = provider.CreateAsyncScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<AverioDbContext>();
            db.Users.Add(new ApplicationUser { Id = id, Email = email, NormalizedEmail = email.ToUpperInvariant(), UserName = email,
                NormalizedUserName = email.ToUpperInvariant(), EmailConfirmed = true, CreatedAtUtc = now, SecurityStamp = Guid.NewGuid().ToString() });
            db.Add(h); db.Add(account); await db.SaveChangesAsync();
        }
        async Task<T> Run<T>(Guid user, Func<IServiceProvider, Task<T>> action) { await using var scope = provider.CreateAsyncScope(); return await action(scope.ServiceProvider); }
        InvitationHandler Handler(IServiceProvider sp, Guid user) => ActivatorUtilities.CreateInstance<InvitationHandler>(sp, new MutableCurrentUser { UserId = user });
        await Run(target.OwnerUserId, async sp => { await Handler(sp, target.OwnerUserId).SendAsync(target.HouseholdId, new(email), null, null, Ct); return true; });
        var token = provider.GetRequiredService<CapturingInvitationSender>().Tokens[email];
        var plan = (await Run(id, sp => Handler(sp, id).PreviewAsync(token, Ct))).Departure!;
        var input = new InvitationAcceptance(token, null, plan.Version, true, null,
            plan.Accounts.Select(x => new AccountCutoverChoice(x.Id, x.Version, x.Balance, "household")).ToArray());
        await using (var write = provider.CreateAsyncScope())
        {
            var sp = write.ServiceProvider; var db = sp.GetRequiredService<AverioDbContext>(); await using var tx = await db.Database.BeginTransactionAsync();
            _ = await sp.GetRequiredService<IHouseholdRepository>().GetByIdForUserForUpdateAsync(h.Id, id, Ct);
            var category = await db.Categories.Where(x => x.IsBase).Select(x => x.Id).FirstAsync();
            db.Add(Transaction.Create(h.Id, account.Id, member, TransactionKind.Income, TransactionVisibility.Private,
                account.Currency, "Concurrent", null, date, [new("Concurrent", category, "Category", "cash", 1)], [], now));
            await db.SaveChangesAsync();
            var acceptance = Run(id, sp2 => Handler(sp2, id).AcceptAsync(input, Ct));
            await tx.CommitAsync();
            await Throws<ConflictException>(() => acceptance);
        }
        plan = (await Run(id, sp => Handler(sp, id).PreviewAsync(token, Ct))).Departure!;
        input = input with { ExpectedSourceVersion = plan.Version, Accounts = plan.Accounts.Select(x => new AccountCutoverChoice(x.Id, x.Version, x.Balance, "household")).ToArray() };
        Check(plan.WillArchive && plan.Accounts.Single().Balance == 11, "Concurrent movement requires a freshly reviewed balance");
        await Run(id, sp => Handler(sp, id).AcceptAsync(input, Ct));
        await Run(id, async sp => {
            var db = sp.GetRequiredService<AverioDbContext>();
            Check(await db.Households.AnyAsync(x => x.Id == h.Id && x.ArchivedAtUtc != null), "Last-member source archived without deletion");
            Check(await db.Set<HouseholdHistory>().AnyAsync(x => x.HouseholdId == h.Id && x.UserId == id), "Last member retains history");
            Check(await db.Accounts.AnyAsync(x => x.HouseholdId == target.HouseholdId && x.Name == "Solo" && x.Scope == AccountScope.Household && x.BalanceAnchorAmount == 11), "Destination handling applied");
            return true;
        });
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task VerifyDepartedWriterAsync(IServiceProvider provider, MutableTimeProvider clock)
    {
        var now = clock.GetUtcNow(); var user = Guid.NewGuid(); var other = Guid.NewGuid();
        var household = Household.CreateForUser(user, now); household.AddMember(other, now);
        await using var setup = provider.CreateAsyncScope();
        var db = setup.ServiceProvider.GetRequiredService<AverioDbContext>();
        foreach (var id in new[] { user, other }) db.Users.Add(new ApplicationUser {
            Id = id, Email = id + "@writer.test", NormalizedEmail = (id + "@writer.test").ToUpperInvariant(),
            UserName = id + "@writer.test", NormalizedUserName = (id + "@writer.test").ToUpperInvariant(),
            EmailConfirmed = true, CreatedAtUtc = now, SecurityStamp = Guid.NewGuid().ToString() });
        db.Add(household); await db.SaveChangesAsync();
        await using var departure = await db.Database.BeginTransactionAsync();
        _ = await setup.ServiceProvider.GetRequiredService<IHouseholdRepository>()
            .GetByIdForUserForUpdateAsync(household.Id, user, Ct);
        await using var writer = provider.CreateAsyncScope();
        var writerDb = writer.ServiceProvider.GetRequiredService<AverioDbContext>();
        await using var write = await writerDb.Database.BeginTransactionAsync();
        var connection = (Npgsql.NpgsqlConnection)writerDb.Database.GetDbConnection();
        var waiting = writer.ServiceProvider.GetRequiredService<IHouseholdRepository>()
            .GetByIdForUserForUpdateAsync(household.Id, user, Ct);
        var didWait = false;
        for (var attempt = 0; attempt < 100 && !didWait; attempt++)
        {
            didWait = await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {connection.ProcessID} AND wait_event_type = 'Lock') AS \"Value\"")
                .SingleAsync();
            if (!didWait) await Task.Delay(20);
        }
        Check(didWait, "Writer must wait behind the departure household lock");
        var successor = household.Members.Single(x => x.UserId == other).Id;
        household.Depart(user, successor, now); await db.SaveChangesAsync();
        household.PromoteSuccessor(successor); await db.SaveChangesAsync();
        await departure.CommitAsync();
        Check(await waiting is null, "Waiting writer loses authorization after its member departs");
    }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
}
