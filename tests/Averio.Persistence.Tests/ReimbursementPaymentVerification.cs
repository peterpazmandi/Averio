using Averio.Application.Common.Exceptions;
using Averio.Application.Common.Abstractions;
using Averio.Application.Recurring;
using Averio.Application.Transactions.CreateTransaction;
using Averio.Application.Transactions.Common;
using Averio.Application.Transactions.VoidTransaction;
using Averio.Application.Transactions.UpdateTransaction;
using Averio.Application.Transactions.Refunds;
using Averio.Application.Imports;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

internal static class ReimbursementPaymentVerification
{
    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture f, MutableTimeProvider clock)
    {
        var ct = CancellationToken.None; user.UserId = f.OwnerUserId;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var config = RecurringReimbursementRule.Create("Settlement provider", ReimbursementCalculation.Fixed, 30, null, f.HouseholdAccountId, 0, null);
        var source = await Post(TransactionKind.Expense, 100, true);
        var claim = await Claim(source);
        var c1 = await Post(TransactionKind.Income, 10); var c2 = await Post(TransactionKind.Income, 20); var tooBig = await Post(TransactionKind.Income, 31);
        await using (var scope = provider.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var batch = Averio.Domain.Imports.ImportBatch.Create(f.HouseholdId, f.OwnerMemberId, new string('C',64), "{}", clock.GetUtcNow(), "settlement.csv");
            batch.Complete(clock.GetUtcNow(), System.Text.Json.JsonSerializer.Serialize(new[] { new { id = c1 } }), "{}");
            db.Add(batch); (await db.Transactions.SingleAsync(t => t.Id == c1)).SetImportOrigin(batch.Id);
            await db.SaveChangesAsync();
        }
        var credit = await Transaction(c1);
        var denied = new LinkReimbursementPayment(c1, Guid.NewGuid(), claim.Version, credit.UpdatedAt, true);
        await Throws<BusinessRuleException>(() => Link(claim.Id, denied));
        await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, claim.Id, claim.Version, true, today, null, ReimbursementStatus.Submitted, ct));
        claim = await Claim(source);
        var before = await FinancialSnapshot();
        var request = denied with { ExpectedVersion = claim.Version, OperationId = Guid.NewGuid() };
        await Throws<BusinessRuleException>(() => Link(claim.Id, request with { MoneyReceived = false }));
        await Throws<ConflictException>(() => Link(claim.Id, request with { ExpectedCreditUpdatedAt = credit.UpdatedAt.AddSeconds(-1) }));
        var options = await Candidates(claim.Id);
        Check(options.Any(t => t.Id == c1) && options.Any(t => t.Id == c2) && options.All(t => t.Id != tooBig), "Candidate eligibility");
        var ids = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Link(claim.Id, request)));
        Check(ids.Distinct().Count() == 1, "Concurrent retry duplicated payment");
        claim = await Claim(source);
        Check(claim.Status == ReimbursementStatus.PartiallyReceived && claim.ReceivedAmount == 10 && claim.OutstandingAmount == 20, "Partial receipt persisted");
        Check((await Candidates(claim.Id)).All(t => t.Id != c1), "Matched credit still selectable");
        await Throws<ConflictException>(() => Link(claim.Id, request with { OperationId = Guid.NewGuid() }));
        await Throws<BusinessRuleException>(() => Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, claim.Id, claim.Version, false, null, null, ReimbursementStatus.Cancelled, ct)));
        await Throws<BusinessRuleException>(() => Use<VoidTransactionHandler>(h => h.HandleAsync(f.HouseholdId, c1, credit.UpdatedAt, ct)));
        var sourceRecord = await Transaction(source);
        await Throws<BusinessRuleException>(() => Use<VoidTransactionHandler>(h => h.HandleAsync(f.HouseholdId, source, sourceRecord.UpdatedAt, ct)));
        await Throws<BusinessRuleException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(new(f.HouseholdId, c1, credit.AccountId, credit.Kind, credit.Visibility,
            credit.Merchant, "Edited", credit.OccurredOn, credit.Items.Select(i => new TransactionItemInput(i.Description, i.CategoryId, i.Amount, i.Id)).ToArray(), [], credit.UpdatedAt), ct)));
        await Throws<BusinessRuleException>(() => Use<ItemRefundHandler>(async h => { await h.RecordAsync(f.HouseholdId, source,
            new(sourceRecord.Items.First().Id, 1, today, Guid.NewGuid(), sourceRecord.UpdatedAt, true), ct); }));
        // Another claim cannot reuse an already allocated credit, even with a fresh version.
        var otherSource = await Post(TransactionKind.Expense, 100, true); var other = await Claim(otherSource);
        await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, other.Id, other.Version, true, today, null, ReimbursementStatus.Submitted, ct));
        other = await Claim(otherSource);
        await Throws<BusinessRuleException>(() => Link(other.Id, request with { OperationId = Guid.NewGuid(), ExpectedVersion = other.Version }));
        // Independent income creation above is outside the reconciliation snapshot.
        before = await FinancialSnapshot();
        var credit2 = await Transaction(c2);
        await Link(claim.Id, new(c2, Guid.NewGuid(), claim.Version, credit2.UpdatedAt, true));
        claim = await Claim(source);
        Check(claim.Status == ReimbursementStatus.Received && claim.OutstandingAmount == 0, "Full receipt persisted");
        Check(await FinancialSnapshot() == before, "Matching changed bank movements, aggregates or Personal Money");
        user.UserId = f.MemberUserId;
        Check((await List(source)).Count == 0, "Other member saw private claim");
        await Throws<NotFoundException>(() => Candidates(claim.Id));
        await Throws<NotFoundException>(() => Unlink(claim.Id, ids[0], claim.Version));
        user.UserId = f.OwnerUserId;
        // Simulate an independent visibility change. Neither list nor badge may expose hidden credit coverage.
        await SetCreditPrivacy(true);
        Check((await List(source)).Count == 0, "Hidden credit leaked claim coverage");
        await using (var scope = provider.CreateAsyncScope()) {
            var statuses = await scope.ServiceProvider.GetRequiredService<IReimbursementRepository>().StatusesAsync(f.HouseholdId, f.OwnerMemberId, [source], ct);
            Check(!statuses.ContainsKey(source), "Badge leaked hidden payment coverage");
        }
        await Throws<NotFoundException>(() => Candidates(claim.Id));
        await SetCreditPrivacy(false);
        var fullVersion = claim.Version;
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Unlink(claim.Id, ids[0], fullVersion)));
        claim = await Claim(source);
        Check(claim.ReceivedAmount == 20 && claim.OutstandingAmount == 10, "Concurrent unlink applied twice");
        Check(await Link(claim.Id, request) == ids[0], "Lost original response recreated removed match");
        var relink = await Link(claim.Id, request with { OperationId = Guid.NewGuid(), ExpectedVersion = claim.Version });
        await Unlink(claim.Id, ids[0], fullVersion);
        claim = await Claim(source);
        Check(claim.Status == ReimbursementStatus.Received && claim.Payments.Count == 3 && relink != ids[0], "Late unlink removed new link");
        Check(await FinancialSnapshot() == before, "Unlink/relink changed financial records");
        // Import undo must protect both active and historical links without disclosing claim details.
        await using (var scope = provider.CreateAsyncScope()) {
            var linked = await scope.ServiceProvider.GetRequiredService<IImportRepository>().LinkedRecordsAsync(f.HouseholdId, [source, c1, c2], ct);
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            Check((await db.Set<Averio.Domain.Imports.ImportBatch>().SingleAsync(b => b.Id == credit.ImportBatchId)).RecordLinks!.Contains(c1.ToString()), "Import batch link changed");
            Check(linked.Contains(source) && linked.Contains(c1) && linked.Contains(c2), "Import undo omitted claim dependencies");
        }
        foreach (var p in claim.Payments.Where(p => p.UnlinkedAt == null)) { var current = await Claim(source); await Unlink(current.Id, p.Id, current.Version); }
        Check((await Transaction(c1)).Status == TransactionStatus.Posted && (await Transaction(c1)).UpdatedAt == credit.UpdatedAt, "Unlink changed original credit");
        claim = await Claim(source);
        Check(claim.Status == ReimbursementStatus.Submitted && claim.ReceivedAmount == 0 && claim.OutstandingAmount == 30, "Fully unlinked claim did not reopen");
        Check(await FinancialSnapshot() == before, "Final unlink changed financial records");
        await using (var scope = provider.CreateAsyncScope()) {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            await Throws<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261120124723_PreserveRefundCreditOrigin"));
            Check(await db.Set<ReimbursementPayment>().AnyAsync(), "Downgrade discarded allocation history");
        }
        var raceSource = await Post(TransactionKind.Expense, 100, true); var raceClaim = await Claim(raceSource);
        await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, raceClaim.Id, raceClaim.Version, true, today, null, ReimbursementStatus.Submitted, ct));
        raceClaim = await Claim(raceSource); claim = await Claim(source);
        var race = await Task.WhenAll(TryLink(claim), TryLink(raceClaim));
        Check(race.Count(x => x) == 1, "Competing claims both allocated the same credit");
        Console.WriteLine("PASS reimbursement payment settlement, retries, dependencies, privacy and unchanged finances");
        async Task<bool> TryLink(ReimbursementClaim candidate) {
            try { await Link(candidate.Id, new(c1, Guid.NewGuid(), candidate.Version, credit.UpdatedAt, true)); return true; }
            catch (BusinessRuleException) { return false; } catch (ConflictException) { return false; }
        }
        async Task SetCreditPrivacy(bool hidden) {
            await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var row = await db.Transactions.SingleAsync(t => t.Id == c1);
            db.Entry(row).Property(t => t.CreatedByHouseholdMemberId).CurrentValue = hidden ? f.MemberId : f.OwnerMemberId;
            db.Entry(row).Property(t => t.Visibility).CurrentValue = hidden ? TransactionVisibility.Private : TransactionVisibility.Household;
            await db.SaveChangesAsync();
        }
        async Task Use<T>(Func<T,Task> action) where T:notnull { await using var s = provider.CreateAsyncScope(); await action(s.ServiceProvider.GetRequiredService<T>()); }
        async Task<Guid> Post(TransactionKind kind, decimal amount, bool track = false) {
            await using var s = provider.CreateAsyncScope();
            return await s.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(new(f.HouseholdId, f.HouseholdAccountId, kind, TransactionVisibility.Household,
                "Settlement test", "Metadata retained", today, [new TransactionItemInput("Settlement", kind == TransactionKind.Expense ? TestIds.Groceries : TestIds.Salary, amount)], [], Reimbursement: track ? config : null), ct);
        }
        async Task<Transaction> Transaction(Guid id) { await using var s = provider.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<AverioDbContext>().Transactions.AsNoTracking().Include(t=>t.Items).SingleAsync(t=>t.Id==id); }
        async Task<IReadOnlyList<ReimbursementResult>> List(Guid id) { await using var s = provider.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ReimbursementHandler>().ListAsync(f.HouseholdId,id,ct); }
        async Task<ReimbursementClaim> Claim(Guid id) => (await List(id)).Single().Claim;
        async Task<IReadOnlyList<Transaction>> Candidates(Guid id) { await using var s = provider.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ReimbursementPaymentHandler>().CandidatesAsync(f.HouseholdId,id,null,0,ct); }
        async Task<Guid> Link(Guid id, LinkReimbursementPayment req) { await using var s = provider.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ReimbursementPaymentHandler>().LinkAsync(f.HouseholdId,id,req,ct); }
        Task Unlink(Guid id,Guid payment,Guid version) => Use<ReimbursementPaymentHandler>(h=>h.UnlinkAsync(f.HouseholdId,id,payment,version,ct));
        async Task<string> FinancialSnapshot() {
            await using var s = provider.CreateAsyncScope(); var db=s.ServiceProvider.GetRequiredService<AverioDbContext>();
            return System.Text.Json.JsonSerializer.Serialize(new { Transactions=await db.Transactions.AsNoTracking().OrderBy(t=>t.Id).Select(t=>new {t.Id,t.Amount,t.Kind,t.Status,t.UpdatedAt,t.AccountId,t.OccurredOn,t.Note,t.ImportBatchId}).ToArrayAsync(),
                Aggregates=await db.TransactionAggregates.AsNoTracking().ToArrayAsync(), Money=await db.PersonalMoneyEntries.AsNoTracking().OrderBy(p=>p.Id).ToArrayAsync() });
        }
    }
    private static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
    private static async Task Throws<T>(Func<Task> action) where T:Exception { try { await action(); } catch(T) { return; } throw new Exception("Expected "+typeof(T).Name); }
}
