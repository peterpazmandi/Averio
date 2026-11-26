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
        Check(options.Any(t => t.Credit.Id == c1) && options.Any(t => t.Credit.Id == c2) && options.Any(t => t.Credit.Id == tooBig && t.AvailableAmount == 31), "Candidate eligibility");
        var ids = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Link(claim.Id, request)));
        Check(ids.Distinct().Count() == 1, "Concurrent retry duplicated payment");
        claim = await Claim(source);
        Check(claim.Status == ReimbursementStatus.PartiallyReceived && claim.ReceivedAmount == 10 && claim.OutstandingAmount == 20, "Partial receipt persisted");
        Check((await Candidates(claim.Id)).All(t => t.Credit.Id != c1), "Matched credit still selectable");
        await Throws<ConflictException>(() => Link(claim.Id, request with { OperationId = Guid.NewGuid() }));
        await Throws<BusinessRuleException>(() => Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, claim.Id, claim.Version, false, null, null, ReimbursementStatus.Cancelled, ct)));
        await Throws<BusinessRuleException>(() => Use<VoidTransactionHandler>(h => h.HandleAsync(f.HouseholdId, c1, credit.UpdatedAt, ct)));
        var sourceRecord = await Transaction(source);
        await Throws<BusinessRuleException>(() => Use<VoidTransactionHandler>(h => h.HandleAsync(f.HouseholdId, source, sourceRecord.UpdatedAt, ct)));
        await Throws<BusinessRuleException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(new(f.HouseholdId, c1, credit.AccountId, credit.Kind, credit.Visibility,
            credit.Merchant, "Edited", credit.OccurredOn, credit.Items.Select(i => new TransactionItemInput(i.Description, i.CategoryId, i.Amount + 1, i.Id)).ToArray(), [], credit.UpdatedAt), ct)));
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
        // One posted credit can cover two claims without a second cash movement.
        var pooledCredit = await Transaction(tooBig);
        var pooledBefore = await FinancialSnapshot();
        var firstAllocation = await Link(other.Id, new(tooBig, Guid.NewGuid(), other.Version, pooledCredit.UpdatedAt, true, 12m));
        other = await Claim(otherSource);
        Check(other.ReceivedAmount == 12m && (await Candidates(other.Id)).All(c => c.Credit.Id != tooBig), "Claim reused its own active credit");
        var thirdSource = await Post(TransactionKind.Expense, 100, true);
        var third = await Claim(thirdSource);
        await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, third.Id, third.Version, true, today, null, ReimbursementStatus.Submitted, ct));
        third = await Claim(thirdSource);
        var remaining = (await Candidates(third.Id)).Single(c => c.Credit.Id == tooBig);
        Check(remaining.AvailableAmount == 19m, "Candidate did not show unallocated credit");
        await Throws<BusinessRuleException>(() => Link(third.Id, new(tooBig, Guid.NewGuid(), third.Version, pooledCredit.UpdatedAt, true, 19.0001m)));
        var secondAllocation = await Link(third.Id, new(tooBig, Guid.NewGuid(), third.Version, pooledCredit.UpdatedAt, true, 19m));
        third = await Claim(thirdSource);
        Check(third.ReceivedAmount == 19m && (await Candidates(other.Id)).All(c => c.Credit.Id != tooBig), "Overallocated pooled credit");
        await Unlink(other.Id, firstAllocation, other.Version);
        Check((await Candidates(other.Id)).Any(c => c.Credit.Id == tooBig && c.AvailableAmount == 12m), "Unlink did not release only its allocation");
        Check((await Claim(thirdSource)).ReceivedAmount == 19m, "Unlink changed another claim's allocation");
        Check((await Transaction(tooBig)).Amount == 31m, "Pooling changed bank credit");
        Check(secondAllocation != firstAllocation && pooledBefore != await FinancialSnapshot(), "Expected independent claim source was not recorded");
        before = await FinancialSnapshot();
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
        // Recovery exposes only opaque IDs for the creator's inaccessible links.
        Check((await Recovery()).Count == 0, "Accessible links incorrectly need recovery");
        Check((await RefundCandidates()).Items.All(t => t.Id != c1), "Refund picker offered a matched reimbursement credit");
        await SetCreditPrivacy(true);
        var recoverable = await Recovery();
        Check(recoverable.Count == 1, "Hidden active link has no recovery path");
        var recoverySnapshot = await FinancialSnapshot();
        user.UserId = f.MemberUserId;
        Check((await Recovery()).Count == 0, "Recovery exposed another member's links");
        await Throws<NotFoundException>(() => Recover(recoverable));
        user.UserId = f.OwnerUserId;
        await Throws<NotFoundException>(() => Recover([recoverable[0], new(recoverable[0].ClaimId, Guid.NewGuid())]));
        Check((await Recovery()).Count == 1, "Failed batch partially removed a link");
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Recover(recoverable)));
        Check((await Recovery()).Count == 0, "Recovery did not release hidden link");
        Check(await FinancialSnapshot() == recoverySnapshot, "Recovery changed bank transactions, aggregates or Personal Money");
        await using (var scope = provider.CreateAsyncScope()) {
            var repo = scope.ServiceProvider.GetRequiredService<IReimbursementRepository>();
            Check(!await repo.HasActivePaymentAsync(f.HouseholdId, c1, ct), "Credit remained protected after recovery");
            var history = await repo.GetOwnedAsync(f.HouseholdId, f.OwnerMemberId, recoverable[0].ClaimId, ct);
            Check(history!.Payments.Single(p => p.Id == recoverable[0].PaymentId).UnlinkedAt != null, "Recovery erased history");
            Check(await repo.GetAsync(f.HouseholdId, f.OwnerMemberId, history.Id, ct) == null, "Recovery revealed hidden history");
            Check((await scope.ServiceProvider.GetRequiredService<IImportRepository>().LinkedRecordsAsync(f.HouseholdId, [c1], ct)).Contains(c1), "Recovery dropped import history protection");
        }
        await SetCreditPrivacy(false);
        Check((await RefundCandidates()).Items.Any(t => t.Id == c1), "Recovered credit unavailable for a refund");
        ReimbursementClaim recovered;
        await using (var scope = provider.CreateAsyncScope()) recovered = (await scope.ServiceProvider.GetRequiredService<IReimbursementRepository>().GetOwnedAsync(f.HouseholdId, f.OwnerMemberId, recoverable[0].ClaimId, ct))!;
        var replacement = await Link(recovered.Id, new(c1, Guid.NewGuid(), recovered.Version, credit.UpdatedAt, true));
        await Recover(recoverable); // Late response/retry must never unlink the replacement.
        await using (var scope = provider.CreateAsyncScope()) {
            var latest = (await scope.ServiceProvider.GetRequiredService<IReimbursementRepository>().GetOwnedAsync(f.HouseholdId, f.OwnerMemberId, recovered.Id, ct))!;
            Check(latest.Payments.Single(p => p.Id == replacement).UnlinkedAt == null, "Late recovery removed a replacement link");
        }
        // Keyset pages contain eligible rows, not pages emptied by client-side dependency filtering.
        var pagedIds = new List<Guid>(); TransactionReadModel? cursor = null;
        for (var n = 0; n < 100; n++) {
            await using var scope = provider.CreateAsyncScope();
            var page = await scope.ServiceProvider.GetRequiredService<ITransactionRepository>().GetVisiblePageAsync(f.HouseholdId, f.OwnerMemberId,
                new(null, null, f.HouseholdAccountId, TransactionKind.Income, null, 1, cursor?.OccurredOn, cursor?.CreatedAt, cursor?.Id, RefundEligibleOnly: true), ct);
            Check(page.Items.Count == 1 || !page.HasMore, "Eligibility filtering produced an empty intermediate page");
            pagedIds.AddRange(page.Items.Select(t => t.Id));
            if (!page.HasMore) break;
            cursor = page.Items.Single();
        }
        Check(pagedIds.SequenceEqual((await RefundCandidates()).Items.Select(t => t.Id)) && !pagedIds.Contains(c1), "Eligibility/keyset pagination mismatch");
        await Throws<BusinessRuleException>(() => Recover([]));
        await Throws<BusinessRuleException>(() => Recover([recoverable[0], recoverable[0]]));
        // Household administration never grants authority over another creator's attribution.
        user.UserId = f.MemberUserId;
        var memberSource = await Post(TransactionKind.Expense, 100, true); var memberClaim = await Claim(memberSource);
        await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, memberClaim.Id, memberClaim.Version, true, today, null, ReimbursementStatus.Submitted, ct));
        memberClaim = await Claim(memberSource);
        Check((await Candidates(memberClaim.Id)).All(x => x.Credit.Id != c2), "Another member's pooled credit was exposed");
        var otherCreditVersion = (await Transaction(c2)).UpdatedAt;
        await Throws<BusinessRuleException>(() => Link(memberClaim.Id, new(c2, Guid.NewGuid(), memberClaim.Version, otherCreditVersion, true)));
        var memberCredit = await Post(TransactionKind.Income, 20);
        var memberLink = await Link(memberClaim.Id, new(memberCredit, Guid.NewGuid(), memberClaim.Version, (await Transaction(memberCredit)).UpdatedAt, true));
        user.UserId = f.OwnerUserId;
        await Throws<NotFoundException>(() => Recover([new(memberClaim.Id, memberLink)]));
        await VerifyLegacyPrivacy();
        await VerifySafeEdits();
        await VerifyVisibilityTransition();
        await VerifyNetExpenseLimit();
        await VerifyDestinationRecovery();
        Console.WriteLine("PASS refund candidate filtering and privacy-safe recovery, atomicity, concurrent retries, history and relinking");
        Console.WriteLine("PASS reimbursement payment settlement, retries, dependencies, privacy and unchanged finances");
        async Task<IReadOnlyList<ReimbursementRecoveryLink>> Recovery() { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ReimbursementPaymentHandler>().RecoveryLinksAsync(f.HouseholdId, ct); }
        async Task VerifyLegacyPrivacy() {
            var sourceId = await Post(TransactionKind.Expense, 100, true);
            var c = await Claim(sourceId);
            await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, c.Id, c.Version, true, today, null, ReimbursementStatus.Submitted, ct));
            c = await Claim(sourceId);
            var creditId = await Post(TransactionKind.Income, 20);
            var request = new LinkReimbursementPayment(creditId, Guid.NewGuid(), c.Version, (await Transaction(creditId)).UpdatedAt, true, 10);
            var linkId = await Link(c.Id, request);
            await using (var scope = provider.CreateAsyncScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                foreach (var row in await db.Transactions.Where(t => t.Id == sourceId || t.Id == creditId).ToArrayAsync())
                    db.Entry(row).Property(t => t.ReimbursementPrivacyEligible).CurrentValue = false;
                await db.SaveChangesAsync();
            }
            Check((await Claim(sourceId)).Payments.Single().Id == linkId, "Legacy payment history disappeared");
            Check(!(await List(sourceId)).Single().CanMatchPayments, "Legacy source offered new matches");
            Check(await Link(c.Id, request) == linkId, "Legacy payment retry lost its result");
            await Throws<BusinessRuleException>(() => Candidates(c.Id));
            var current = await Claim(sourceId);
            await Throws<BusinessRuleException>(() => Link(c.Id, request with { OperationId = Guid.NewGuid(), ExpectedVersion = current.Version }));
            await Unlink(c.Id, linkId, current.Version);
            var freshSource = await Post(TransactionKind.Expense, 100, true);
            var fresh = await Claim(freshSource);
            await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, fresh.Id, fresh.Version, true, today, null, ReimbursementStatus.Submitted, ct));
            fresh = await Claim(freshSource);
            Check((await Candidates(fresh.Id)).All(x => x.Credit.Id != creditId), "Legacy credit appeared as a candidate");
            await Throws<BusinessRuleException>(() => Link(fresh.Id, new(creditId, Guid.NewGuid(), fresh.Version, request.ExpectedCreditUpdatedAt, true, 1)));
        }
        Task Recover(IReadOnlyList<ReimbursementRecoveryLink> links) => Use<ReimbursementPaymentHandler>(h => h.RecoverAsync(f.HouseholdId, links, ct));
        async Task<TransactionReadPage> RefundCandidates() { await using var scope = provider.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<ITransactionRepository>().GetVisiblePageAsync(f.HouseholdId, f.OwnerMemberId, new(null, null, f.HouseholdAccountId, TransactionKind.Income, null, 100, null, null, null, RefundEligibleOnly: true), ct); }

        async Task VerifyNetExpenseLimit() {
            var sourceId = await Post(TransactionKind.Expense, 100, true);
            var sourceRecord = await Transaction(sourceId);
            await Use<ItemRefundHandler>(async h => { await h.RecordAsync(f.HouseholdId, sourceId,
                new(sourceRecord.Items.Single().Id, 80, today, Guid.NewGuid(), sourceRecord.UpdatedAt, true), ct); });
            var c = await Claim(sourceId);
            await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, c.Id, c.Version, true, today, null, ReimbursementStatus.Submitted, ct));
            c = await Claim(sourceId);
            var creditId = await Post(TransactionKind.Income, 30);
            var creditRecord = await Transaction(creditId);
            var candidate = (await Candidates(c.Id)).Single(x => x.Credit.Id == creditId);
            Check(candidate.AvailableAmount == 30 && candidate.MaximumAllocation == 20, "Candidate omitted the net expense limit");
            await Throws<BusinessRuleException>(() => Link(c.Id, new(creditId, Guid.NewGuid(), c.Version, creditRecord.UpdatedAt, true, 20.0001m)));
            await Link(c.Id, new(creditId, Guid.NewGuid(), c.Version, creditRecord.UpdatedAt, true, candidate.MaximumAllocation));
        }

        async Task VerifyDestinationRecovery() {
            var destination = Averio.Domain.Accounts.Account.Create(f.HouseholdId, [f.OwnerMemberId, f.MemberId], "Recovery destination",
                Averio.Domain.Accounts.AccountType.Cash, Averio.Domain.Accounts.AccountScope.Personal,
                Averio.Domain.Accounts.AccountVisibility.Household, true, Averio.Domain.Common.CurrencyCode.Eur, 0, today, clock.GetUtcNow());
            await using (var scope = provider.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); db.Add(destination); await db.SaveChangesAsync(ct); }
            var sourceId = await Post(TransactionKind.Expense, 100);
            var sourceRecord = await Transaction(sourceId);
            var destinationConfig = RecurringReimbursementRule.Create("Private destination", ReimbursementCalculation.Fixed, 10, null, destination.Id, 0, null);
            await Use<ReimbursementHandler>(async h => { await h.CreateAsync(f.HouseholdId, sourceId, sourceRecord.UpdatedAt, destinationConfig, ct); });
            var c = await Claim(sourceId);
            await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, c.Id, c.Version, true, today, null, ReimbursementStatus.Submitted, ct));
            c = await Claim(sourceId);
            Guid creditId;
            await using (var scope = provider.CreateAsyncScope()) creditId = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, destination.Id, TransactionKind.Income, TransactionVisibility.Household, "Recovery credit", null, today,
                    [new("Recovery credit", TestIds.Salary, 10)], []), ct);
            var paymentId = await Link(c.Id, new(creditId, Guid.NewGuid(), c.Version, (await Transaction(creditId)).UpdatedAt, true));
            c = await Claim(sourceId);
            await using (var scope = provider.CreateAsyncScope()) {
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                var account = await db.Accounts.Include(a => a.Owners).SingleAsync(a => a.Id == destination.Id, ct);
                account.Update(account.Name, account.Type, [f.MemberId], Averio.Domain.Accounts.AccountVisibility.Private, false, clock.GetUtcNow());
                await db.SaveChangesAsync(ct);
            }
            await using (var scope = provider.CreateAsyncScope()) {
                var handler = scope.ServiceProvider.GetRequiredService<ReimbursementHandler>();
                Check((await handler.DestinationRecoveryAsync(f.HouseholdId, ct)).All(x => x.ClaimId != c.Id), "Active link appeared in destination recovery");
                await Throws<BusinessRuleException>(() => handler.ReassignDestinationAsync(f.HouseholdId, c.Id, c.Version, f.HouseholdAccountId, ct));
            }
            await Use<ReimbursementPaymentHandler>(h => h.RecoverAsync(f.HouseholdId, [new(c.Id, paymentId)], ct));
            await using (var scope = provider.CreateAsyncScope()) {
                var handler = scope.ServiceProvider.GetRequiredService<ReimbursementHandler>();
                var recovery = (await handler.DestinationRecoveryAsync(f.HouseholdId, ct)).Single(x => x.ClaimId == c.Id);
                await handler.ReassignDestinationAsync(f.HouseholdId, c.Id, recovery.Version, f.HouseholdAccountId, ct);
            }
            Check((await List(sourceId)).Count == 0, "Destination recovery exposed an inaccessible historical credit");
        }

        async Task VerifyVisibilityTransition() {
            var account = Averio.Domain.Accounts.Account.Create(f.HouseholdId, [f.OwnerMemberId], "Visibility transition", Averio.Domain.Accounts.AccountType.Cash,
                Averio.Domain.Accounts.AccountScope.Personal, Averio.Domain.Accounts.AccountVisibility.Household, true, Averio.Domain.Common.CurrencyCode.From("EUR"), 0, today, clock.GetUtcNow());
            await using (var scope = provider.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); db.Add(account); await db.SaveChangesAsync(); }
            Guid expenseId, creditId;
            await using (var scope = provider.CreateAsyncScope()) expenseId = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, account.Id, TransactionKind.Expense, TransactionVisibility.Household, "Visibility test", null, today, [new("Visibility test", TestIds.Groceries, 100)], [],
                    Reimbursement: RecurringReimbursementRule.Create("Visibility test", ReimbursementCalculation.Fixed, 10, null, account.Id, 0, null)), ct);
            await using (var scope = provider.CreateAsyncScope()) creditId = await scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, account.Id, TransactionKind.Income, TransactionVisibility.Household, "Visibility credit", null, today, [new("Visibility credit", TestIds.Salary, 10)], []), ct);
            var c = await Claim(expenseId);
            await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, c.Id, c.Version, true, today, null, ReimbursementStatus.Submitted, ct)); c = await Claim(expenseId);
            await Link(c.Id, new(creditId, Guid.NewGuid(), c.Version, (await Transaction(creditId)).UpdatedAt, true));
            c = await Claim(expenseId);
            await Use<Averio.Application.Accounts.UpdateAccount.UpdateAccountHandler>(h => h.HandleAsync(new(f.HouseholdId, account.Id, account.Name, account.Type, [f.OwnerMemberId], Averio.Domain.Accounts.AccountVisibility.Private, false, account.UpdatedAt), ct));
            var before = await StableFinancials();
            foreach (var id in new[] { expenseId, creditId }) {
                var row = await Transaction(id);
                var command = new UpdateTransactionCommand(f.HouseholdId, id, account.Id, row.Kind, row.Visibility, row.Merchant, "Only a note", row.OccurredOn,
                    row.Items.Select(i => new TransactionItemInput(i.Description,i.CategoryId,i.OriginalAmount,i.Id)).ToArray(), [],row.UpdatedAt);
                await Use<UpdateTransactionHandler>(h => h.HandleAsync(command, ct));
                var updated = await Transaction(id);
                Check(updated.Note == "Only a note" && updated.Visibility == TransactionVisibility.Household, "Private account prevented safe edit or rewrote stored visibility");
                command = command with { ExpectedUpdatedAt = updated.UpdatedAt };
                await Throws<BusinessRuleException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(command with { Visibility = TransactionVisibility.Private }, ct)));
                await Throws<BusinessRuleException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(command with { Items = command.Items.Select(i => i with { Amount = i.Amount + 1 }).ToArray() }, ct)));
                user.UserId = f.MemberUserId;
                await Throws<NotFoundException>(() => Use<Averio.Application.Transactions.GetTransaction.GetTransactionHandler>(async h => { await h.HandleAsync(f.HouseholdId,id,ct); }));
                await Throws<NotFoundException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(command, ct)));
                await using (var scope = provider.CreateAsyncScope()) {
                    var page = await scope.ServiceProvider.GetRequiredService<ITransactionRepository>().GetVisiblePageAsync(f.HouseholdId, f.MemberId, new(null,null,account.Id,null,null,100,null,null,null),ct);
                    Check(page.Items.Count == 0, "Preserved visibility exposed private account rows");
                }
                user.UserId = f.OwnerUserId;
            }
            Check(await StableFinancials() == before, "Visibility transition edits changed finances");
            var after = await Claim(expenseId);
            Check(after.Version == c.Version && after.ReceivedAmount == c.ReceivedAmount, "Visibility transition edits changed claim coverage");
            await using (var scope = provider.CreateAsyncScope()) await Throws<BusinessRuleException>(() => scope.ServiceProvider.GetRequiredService<CreateTransactionHandler>().HandleAsync(
                new(f.HouseholdId, account.Id, TransactionKind.Income, TransactionVisibility.Household, "Invalid new credit", null, today, [new("Invalid new credit", TestIds.Salary, 10)], []),ct));
            Console.WriteLine("PASS safe edits after account privacy change: preserved visibility, protected amounts, private reads/writes and unchanged coverage/finances");
        }
        async Task VerifySafeEdits() {
            var expenseId = await Post(TransactionKind.Expense, 100, true);
            var expense = await Transaction(expenseId);
            // A prior merchant refund means edit inputs carry original item amounts, not net cost.
            await Use<ItemRefundHandler>(async h => { await h.RecordAsync(f.HouseholdId, expenseId,
                new(expense.Items.Single().Id, 20, today, Guid.NewGuid(), expense.UpdatedAt, true), ct); });
            var receiptId = await Post(TransactionKind.Income, 30);
            var c = await Claim(expenseId);
            await Use<ReimbursementHandler>(h => h.ChangeAsync(f.HouseholdId, c.Id, c.Version, true, today, null, ReimbursementStatus.Submitted, ct));
            c = await Claim(expenseId);
            var paymentId = await Link(c.Id, new(receiptId, Guid.NewGuid(), c.Version, (await Transaction(receiptId)).UpdatedAt, true));
            c = await Claim(expenseId);
            var claimVersion = c.Version;
            var alternate = Averio.Domain.Accounts.Account.Create(f.HouseholdId, [], "Alternative account", Averio.Domain.Accounts.AccountType.Cash,
                Averio.Domain.Accounts.AccountScope.Household, Averio.Domain.Accounts.AccountVisibility.Household, true, Averio.Domain.Common.CurrencyCode.From("EUR"), 0, today, clock.GetUtcNow());
            await using (var scope = provider.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>(); db.Add(alternate); await db.SaveChangesAsync(); }
            var cashBefore = await StableFinancials();
            foreach (var id in new[] { expenseId, receiptId }) {
                var row = await Transaction(id);
                var command = new UpdateTransactionCommand(f.HouseholdId, id, row.AccountId, row.Kind, row.Visibility,
                    "Corrected merchant", "Corrected note", row.OccurredOn,
                    row.Items.Select(i => new TransactionItemInput("Corrected item", i.CategoryId, i.OriginalAmount, i.Id)).ToArray(), [], row.UpdatedAt);
                await using (var scope = provider.CreateAsyncScope()) {
                    var detail = await scope.ServiceProvider.GetRequiredService<Averio.Application.Transactions.GetTransaction.GetTransactionHandler>().HandleAsync(f.HouseholdId, id, ct);
                    Check(detail.PaymentDetailsLocked, "Detail omitted immediate payment protection");
                }
                foreach (var invalid in new[] {
                    command with { OccurredOn = today.AddDays(-1) },
                    command with { AccountId = alternate.Id },
                    command with { Visibility = TransactionVisibility.Private },
                    command with { Kind = row.Kind == TransactionKind.Income ? TransactionKind.Expense : TransactionKind.Income },
                    command with { Items = command.Items.Select(i => i with { Amount = i.Amount + 1 }).ToArray() },
                    command with { Items = command.Items.Select(i => i with { ExistingItemId = null }).ToArray() },
                    command with { Items = command.Items.Select(i => i with { PersonalMoneyAmount = 0 }).ToArray() },
                    command with { Items = [..command.Items, new TransactionItemInput("New item", row.Items.Single().CategoryId, 1)] }
                }) await Throws<BusinessRuleException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(invalid, ct)));
                await Use<UpdateTransactionHandler>(h => h.HandleAsync(command, ct));
                var updated = await Transaction(id);
                Check(updated.Note == "Corrected note" && updated.Merchant == "Corrected merchant" && updated.Items.Single().Description == "Corrected item", "Safe edits did not persist");
                Check(updated.Amount == row.Amount && updated.Items.Single().Id == row.Items.Single().Id, "Safe edit changed monetary value or item identity");
                await Throws<ConflictException>(() => Use<UpdateTransactionHandler>(h => h.HandleAsync(command, ct)));
                // Saving an unchanged transaction is valid too.
                await Use<UpdateTransactionHandler>(h => h.HandleAsync(command with { ExpectedUpdatedAt = updated.UpdatedAt }, ct));
            }
            Check(await StableFinancials() == cashBefore, "Descriptive edits changed cash, totals or Personal Money");
            var after = await Claim(expenseId);
            Check(after.Version == claimVersion && after.ReceivedAmount == 30 && after.Payments.Single().Id == paymentId, "Safe edits changed reconciliation");
            // A category correction moves category totals but leaves account totals and coverage intact.
            Guid category;
            await using (var scope = provider.CreateAsyncScope()) category = await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Categories
                .Where(x => x.IsBase && x.Kind == Averio.Domain.Categories.CategoryKind.Expense && x.Id != TestIds.Groceries).Select(x => x.Id).FirstAsync();
            var original = await Transaction(expenseId);
            decimal priorCategory;
            await using (var scope = provider.CreateAsyncScope()) priorCategory = await scope.ServiceProvider.GetRequiredService<AverioDbContext>().TransactionAggregates
                .Where(a => a.HouseholdId == f.HouseholdId && a.AccountId == f.HouseholdAccountId && a.DimensionType == TransactionAggregateDimension.Category && a.DimensionId == category && a.Period == TransactionAggregatePeriod.Day).SumAsync(a => a.ExpenseAmount);
            await Use<UpdateTransactionHandler>(h => h.HandleAsync(new(f.HouseholdId, expenseId, original.AccountId, original.Kind, original.Visibility, original.Merchant, original.Note, original.OccurredOn,
                [new("Corrected item", category, original.Items.Single().OriginalAmount, original.Items.Single().Id)], [], original.UpdatedAt), ct));
            await using (var scope = provider.CreateAsyncScope()) {
                var total = await scope.ServiceProvider.GetRequiredService<AverioDbContext>().TransactionAggregates
                    .Where(a => a.HouseholdId == f.HouseholdId && a.AccountId == f.HouseholdAccountId && a.DimensionType == TransactionAggregateDimension.Category && a.DimensionId == category && a.Period == TransactionAggregatePeriod.Day).SumAsync(a => a.ExpenseAmount);
                Check(total == priorCategory + 80, "Category correction did not move net expense");
            }
            Check(await StableFinancials() == cashBefore, "Recategorization changed account totals or Personal Money");
            await Unlink(c.Id, paymentId, after.Version);
            await using (var scope = provider.CreateAsyncScope()) {
                var detail = await scope.ServiceProvider.GetRequiredService<Averio.Application.Transactions.GetTransaction.GetTransactionHandler>().HandleAsync(f.HouseholdId, receiptId, ct);
                Check(!detail.PaymentDetailsLocked, "Unlink retained protection");
            }
            Console.WriteLine("PASS linked source/credit safe edits, protected fields, unchanged saves, stale edits, refund net values and category totals");
        }
        async Task<string> StableFinancials() {
            await using var scope = provider.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            return System.Text.Json.JsonSerializer.Serialize(new {
                Rows = await db.Transactions.OrderBy(t => t.Id).Select(t => new {t.Id,t.AccountId,t.Kind,t.Amount,t.Status,t.OccurredOn,t.Visibility,t.ImportBatchId}).ToArrayAsync(),
                Totals = await db.TransactionAggregates.Where(a => a.DimensionType == TransactionAggregateDimension.Account).OrderBy(a => a.AccountId).ThenBy(a => a.Period).ThenBy(a => a.PeriodStart).Select(a => new {a.AccountId,a.Period,a.PeriodStart,a.IncomeAmount,a.ExpenseAmount,a.EntryCount}).ToArrayAsync(),
                Money = await db.PersonalMoneyEntries.OrderBy(p => p.Id).ToArrayAsync()
            });
        }
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
        async Task<IReadOnlyList<ReimbursementCreditCandidate>> Candidates(Guid id) { await using var s = provider.CreateAsyncScope(); return await s.ServiceProvider.GetRequiredService<ReimbursementPaymentHandler>().CandidatesAsync(f.HouseholdId,id,null,0,ct); }
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

