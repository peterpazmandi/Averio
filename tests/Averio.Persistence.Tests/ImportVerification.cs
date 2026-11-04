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
        async Task<ImportHistoryDetail> Detail(Guid? id = null, int page = 0)
        {
            using var scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ImportHandler>().HistoryDetailAsync(household.Id, id ?? preview.Id, page, default);
        }
        var original = await Detail();
        Check(original.Review.Unchanged == 4 && original.Records.All(r => r.RecordId != null && r.Changed == false), "new import revisions survive PostgreSQL precision");
        Check(original.Batch.Summary.Count == 4 && original.Batch.TrackingAvailable, "history counts and tracking");
        Check(original.Batch.Summary.Totals.Single(t => t.Kind == "Expense").Amount == "2469.1356", "history totals keep precision");
        Check(!(await Detail(page: 1)).Records.Any(), "history records pagination");
        using (var scope = provider.CreateScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<ImportHandler>();
            Check((await handler.HistoryAsync(household.Id, 0, default)).Items.Single().Id == preview.Id, "history contains completed import");
            Check(!(await handler.HistoryAsync(household.Id, 1, default)).Items.Any(), "history pagination");
        }
        user.UserId = partnerUser.Id;
        using (var scope = provider.CreateScope())
            Check(!(await scope.ServiceProvider.GetRequiredService<ImportHandler>().HistoryAsync(household.Id, 0, default)).Items.Any(), "history is uploader-private");
        await Reject<NotFoundException>(() => Detail());
        user.UserId = ownerUser.Id;
        Guid legacyId;
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var records = await db.Transactions.Where(t => t.HouseholdId == household.Id).OrderBy(t => t.Id).ToArrayAsync();
            records[0].MarkAllocationChanged(now.AddMinutes(1));
            records[1].SetArchived(true, now.AddMinutes(1));
            records[2].Void(now.AddMinutes(1));
            var transfer = await db.Transfers.SingleAsync(t => t.HouseholdId == household.Id);
            transfer.Update(transfer.SentAmount, transfer.ReceivedAmount, transfer.OccurredOn, "Changed later", now.AddMinutes(1));
            var batch = await db.Set<ImportBatch>().SingleAsync(b => b.Id == preview.Id);
            var legacy = ImportBatch.Create(household.Id, member.Id, new string('A', 64), batch.Payload, now);
            legacy.Complete(now); legacyId = legacy.Id; db.Add(legacy);
            await db.SaveChangesAsync();
        }
        var changed = await Detail();
        Check(changed.Review.Edited == 3 && changed.Review.Voided == 1, "archive, revision and transfer edits flagged; void distinct");
        Check(changed.Batch.Summary.Totals.SequenceEqual(original.Batch.Summary.Totals), "original totals survive edits");
        var legacyDetail = await Detail(legacyId);
        Check(!legacyDetail.Batch.TrackingAvailable && legacyDetail.Review.Untracked == 4
            && legacyDetail.Records.All(r => r.RecordId == null && r.Changed == null), "legacy import never guesses record links");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            // Hide the source account: neither transaction nor transfer IDs may leak.
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE accounts SET scope = 'Personal', visibility = 'Private' WHERE id = {main.Id}");
        }
        var hidden = await Detail();
        Check(hidden.Review.Unavailable == 4 && hidden.Records.All(r => r.RecordId == null && r.Changed == null), "inaccessible records redact current identity and revision");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check(await db.Transactions.CountAsync(t => t.HouseholdId == household.Id) == 3
                && await db.Transfers.CountAsync(t => t.HouseholdId == household.Id) == 1, "preview never deletes records");
            Check((await db.Accounts.SingleAsync(a => a.Id == main.Id)).BalanceAnchorAmount == main.BalanceAnchorAmount, "preview never changes balance anchors");
        }
        Console.WriteLine("PASS import history precision, privacy, revisions, pagination, legacy fallback and read-only preview");
        Console.WriteLine("PASS XLSX concurrent preview/confirmation, privacy, mapping rollback, aggregates and balance anchors");
    }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}

