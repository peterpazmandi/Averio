using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Imports;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Averio.Domain.Imports;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Averio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ImportVerification
{
    public static async Task RunAsync(ServiceProvider provider, MutableCurrentUser user, Fixture fixture, DateTimeOffset now)
    {
        ApplicationUser NewUser(string name) => new()
        {
            Id = Guid.NewGuid(), UserName = name + "@imports.test",
            NormalizedUserName = (name + "@imports.test").ToUpperInvariant(),
            Email = name + "@imports.test", NormalizedEmail = (name + "@imports.test").ToUpperInvariant(),
            EmailConfirmed = true, DisplayName = name, PreferredCulture = "en-US",
            CreatedAtUtc = now, SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        var ownerUser = NewUser("import-owner");
        var partnerUser = NewUser("import-partner");
        user.UserId = ownerUser.Id;
        var household = Household.CreateForUser(ownerUser.Id, now);
        household.CompleteOnboarding("Import checks", "DE", CurrencyCode.Eur, now);
        household.CompleteFinancialSetup(now);
        var member = household.Members.Single();
        var main = Account.Create(household.Id, [], "Main", AccountType.Checking, AccountScope.Household,
            AccountVisibility.Household, true, CurrencyCode.Eur, 10000m, new DateOnly(2026, 9, 1), now);
        var savings = Account.Create(household.Id, [member.Id], "Savings", AccountType.Savings, AccountScope.Personal,
            AccountVisibility.Private, false, CurrencyCode.Eur, 500m, new DateOnly(2026, 10, 1), now);
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            db.Users.AddRange(ownerUser, partnerUser);
            db.Households.Add(household); db.Accounts.AddRange(main, savings);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO household_members (id, household_id, user_id, role, joined_at_utc) VALUES ({Guid.NewGuid()}, {household.Id}, {partnerUser.Id}, {"member"}, {now})");
        }
        async Task<ImportPreview> Preview()
        {
            using var scope = provider.CreateScope(); using var file = ImportChecks.Workbook();
            return await scope.ServiceProvider.GetRequiredService<ImportHandler>().PreviewAsync(household.Id, file, default);
        }
        async Task<ImportResult> Confirm(Guid id, ConfirmImportRequest request)
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ImportHandler>().ConfirmAsync(household.Id, id, request, default);
        }
        var previews = await Task.WhenAll(Preview(), Preview());
        var preview = previews[0];
        Check(preview.Id == previews[1].Id && preview.RepeatedRows == 1 && preview.Count == 4, "idempotent concurrent preview");
        var request = new ConfirmImportRequest([
            new("Main", main.Id, "Household"), new("Savings", savings.Id, "Private")],
            preview.Categories.Select(c => new ImportCategoryMapping(c.Kind, c.Name, null)).ToArray(), true);
        user.UserId = partnerUser.Id;
        await Reject<NotFoundException>(() => Confirm(preview.Id, request));
        using (var scope = provider.CreateScope())
            await Reject<NotFoundException>(() => scope.ServiceProvider.GetRequiredService<ImportHandler>().GetAsync(household.Id, preview.Id, default));
        user.UserId = ownerUser.Id;
        // Invalid visibility is rejected before any financial data is committed.
        await Reject<BusinessRuleException>(() => Confirm(preview.Id, request with { Accounts = [new("Main", main.Id, "Private"), new("Savings", savings.Id, "Private")] }));
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(!await db.Transactions.AnyAsync(t => t.HouseholdId == household.Id), "failed import leaves no transactions");
            Check(!await db.Categories.AnyAsync(c => c.HouseholdId == household.Id), "failed import leaves no categories");
        }
        var results = await Task.WhenAll(Confirm(preview.Id, request), Confirm(preview.Id, request));
        Check(results.All(r => r.Transactions == 3 && r.Transfers == 1), "idempotent concurrent confirmation");
        Check((await Preview()).Status == "Completed", "re-upload recognizes completed batch");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(await db.Transactions.CountAsync(t => t.HouseholdId == household.Id) == 3, "repeated rows preserved exactly once");
            Check(await db.Transfers.CountAsync(t => t.HouseholdId == household.Id) == 1, "single transfer");
            Check(await db.Categories.CountAsync(c => c.HouseholdId == household.Id) == 2, "categories created exactly once");
            Check(await db.Set<ImportBatch>().CountAsync(b => b.HouseholdId == household.Id && b.CompletedAt != null) == 1, "completed batch persisted");
            var aggregate = await db.TransactionAggregates.SingleAsync(a => a.HouseholdId == household.Id
                && a.DimensionType == TransactionAggregateDimension.Account && a.Period == TransactionAggregatePeriod.Day);
            Check(aggregate.EntryCount == 3 && aggregate.ExpenseAmount == 2469.1356m && aggregate.IncomeAmount == 1234.5678m, "bulk aggregate precision and counts");
            var balances = await scope.ServiceProvider.GetRequiredService<IAccountBalanceReader>().GetCurrentBalancesAsync(
                household.Id, member.Id, [main.Id, savings.Id], new DateOnly(2026, 11, 3), default);
            Check(balances[main.Id] == 8755.4322m && balances[savings.Id] == 500m, "balance anchors remain authoritative");
        }
        Console.WriteLine("PASS XLSX concurrent preview/confirmation, privacy, mapping rollback, aggregates and balance anchors");
    }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}
