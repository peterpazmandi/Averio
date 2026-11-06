using System.Text.Json;
using Averio.Application.Imports;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using Averio.Domain.Activities;
using Averio.Domain.Common;
using Averio.Domain.Imports;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class ImportImprovementVerification
{
    public static async Task RunAsync(ServiceProvider provider, Guid householdId, Guid memberId, DateTimeOffset now)
    {
        var a = Account.Create(householdId, [], "Review A", AccountType.Checking, AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 100m, new DateOnly(2026, 1, 1), now);
        var b = Account.Create(householdId, [], "Review B", AccountType.Savings, AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 50m, new DateOnly(2026, 1, 1), now);
        using (var scope = provider.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); db.Accounts.AddRange(a, b); await db.SaveChangesAsync(); }
        var day = new DateOnly(2026, 9, 21);
        ImportRow Row(int i, decimal amount, string kind = "Expense") => new("Source", i, kind, day, "Review A", kind == "Transfer" ? "Review B" : null, kind == "Transfer" ? "" : "Review category", amount, "EUR", null, null, "Shop", "note");
        ImportRow[] originalRows = [Row(1, 5), Row(2, 5), Row(3, 6), Row(4, 20, "Income"), Row(5, 2, "Transfer")];
        async Task<Guid> Draft(ImportRow[] rows)
        {
            using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var batch = ImportBatch.Create(householdId, memberId, Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), JsonSerializer.Serialize(rows), now);
            db.Add(batch); await db.SaveChangesAsync(); return batch.Id;
        }
        async Task<T> Call<T>(Func<ImportHandler, Task<T>> action) { using var scope = provider.CreateScope(); return await action(scope.ServiceProvider.GetRequiredService<ImportHandler>()); }
        var mappings = new ImportAccountMapping[] { new("Review A", a.Id, "Household"), new("Review B", b.Id, "Household") };
        var request = new ConfirmImportRequest(mappings, [new("Expense", "Review category", null), new("Income", "Review category", null)], true);
        var first = await Draft(originalRows);
        var firstReview = await Call(h => h.ReviewAsync(householdId, first, new(mappings), default));
        Check(firstReview.Overlaps.Count == 0, "repeated rows inside a new file are not existing duplicates");
        var completed = await Call(h => h.ConfirmAsync(householdId, first, request with { ReviewToken = firstReview.Token }, default));
        Check(completed.Transactions == 4 && completed.Transfers == 1 && completed.Accounts == 2, "completion counts include transfers and distinct accounts");
        using (var stream = ImportChecks.Workbook())
        {
            var preview = await Call(h => h.PreviewAsync(householdId, stream, default));
            Check(preview.Defaults!.Accounts.Any(m => m.Source == "Review A" && m.AccountId == a.Id) && preview.Defaults.Categories.Any(c => c.Source == "Review category" && c.CategoryId != null), "mappings remembered after successful import");
        }
        var balance = await Call(h => h.BalancesAsync(householdId, first, day, default));
        Check(decimal.Parse(balance.Single(x => x.AccountId == a.Id).CurrentBalance, System.Globalization.CultureInfo.InvariantCulture) == 102m && decimal.Parse(balance.Single(x => x.AccountId == b.Id).CurrentBalance, System.Globalization.CultureInfo.InvariantCulture) == 52m, "reconciliation includes both transfer legs and precision");
        var overlap = await Draft([Row(6, 5), Row(7, 99)]);
        var oneAccount = new[] { mappings[0] };
        var overlapReview = await Call(h => h.ReviewAsync(householdId, overlap, new(oneAccount), default));
        Check(overlapReview.Overlaps.Single().RowIndex == 0 && overlapReview.Overlaps.Single().MatchCount == 2, "cross-file overlap matches legitimate repeated records without auto-skipping");
        var overlapRequest = new ConfirmImportRequest(oneAccount, [new("Expense", "Review category", null)], true, "stale", [0]);
        await Reject<ConflictException>(() => Call(h => h.ConfirmAsync(householdId, overlap, overlapRequest, default)));
        var skipped = await Call(h => h.ConfirmAsync(householdId, overlap, overlapRequest with { ReviewToken = overlapReview.Token }, default));
        Check(skipped.Transactions == 1 && skipped.Skipped == 1, "explicit skip creates only the remaining record");
        var history = await Call(h => h.HistoryDetailAsync(householdId, overlap, 0, default));
        Check(history.Records[0].State == "Skipped" && history.Batch.Summary.Count == 1, "skipped rows remain auditable but are excluded from imported totals");
        var keep = await Draft([Row(8, 5)]);
        var keepReview = await Call(h => h.ReviewAsync(householdId, keep, new(oneAccount), default));
        Check((await Call(h => h.ConfirmAsync(householdId, keep, overlapRequest with { ReviewToken = keepReview.Token, SkippedRows = [] }, default))).Transactions == 1, "explicit keep preserves a legitimate duplicate");
        var stale = await Call(h => h.UndoPlanAsync(householdId, first, default));
        Check(stale.Eligible == 5, "fresh records initially eligible for undo");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var expenses = await db.Transactions.Where(t => t.ImportBatchId == first && t.Amount == 5).OrderBy(t => t.Id).ToArrayAsync();
            expenses[0].SetArchived(true, now.AddMinutes(1));
            var activity = Activity.Create(householdId, memberId, "Keep linked", null, null, now);
            var assignment = ActivityAssignment.Create(householdId, memberId, expenses[1].Id); assignment.Assign(activity, now);
            db.Add(activity); db.Add(assignment); await db.SaveChangesAsync();
        }
        await Reject<ConflictException>(() => Call(h => h.UndoAsync(householdId, first, new(stale.Token, true), default)));
        var plan = await Call(h => h.UndoPlanAsync(householdId, first, default));
        Check(plan.Eligible == 3 && plan.Records.Count(r => r.State == "Edited") == 1 && plan.Records.Count(r => r.State == "Linked") == 1, "undo retains edited and activity-linked records");
        Check(decimal.Parse(plan.Effects.Single(e => e.AccountId == a.Id).Change, System.Globalization.CultureInfo.InvariantCulture) == -12m, "undo previews current balance effect");
        var undone = await Call(h => h.UndoAsync(householdId, first, new(plan.Token, true), default));
        Check(undone.Voided == 3 && undone.Retained == 2, "safe partial undo");
        Check((await Call(h => h.UndoPlanAsync(householdId, first, default))).Eligible == 0, "repeated undo cannot reverse again");
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var aggregate = await db.TransactionAggregates.SingleAsync(x => x.AccountId == a.Id && x.DimensionType == TransactionAggregateDimension.Account && x.Period == TransactionAggregatePeriod.Day);
            Check(aggregate.IncomeAmount == 0 && aggregate.ExpenseAmount == 114 && aggregate.EntryCount == 4, "undo subtracts only eligible aggregates");
            Check((await db.Accounts.SingleAsync(x => x.Id == a.Id)).BalanceAnchorAmount == 100, "reconciliation and undo preserve balance reference");
        }
        Console.WriteLine("PASS import overlap decisions, remembered mappings, reconciliation, completion, stale undo, linked-record protection and aggregate reversal");
    }
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    private static async Task Reject<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
}