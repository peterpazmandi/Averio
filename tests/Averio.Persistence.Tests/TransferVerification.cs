using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transfers;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class TransferVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly DateOnly Date = new(2026, 9, 21);

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user, Fixture fixture, MutableTimeProvider clock)
    {
        user.UserId = fixture.OwnerUserId;
        Guid destinationId;
        Guid fxId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var destination = Account.Create(fixture.HouseholdId, [], "Transfer savings", AccountType.Savings,
                AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Eur, 0m,
                new DateOnly(2026, 9, 1), clock.GetUtcNow());
            var fx = Account.Create(fixture.HouseholdId, [], "Transfer USD", AccountType.Savings,
                AccountScope.Household, AccountVisibility.Household, true, CurrencyCode.Usd, 0m,
                new DateOnly(2026, 9, 1), clock.GetUtcNow());
            db.Accounts.AddRange(destination, fx);
            await db.SaveChangesAsync();
            destinationId = destination.Id; fxId = fx.Id;
        }
        var baseline = await BalanceAsync(provider, fixture, fixture.HouseholdAccountId);
        var aggregatesBefore = await AggregateFingerprintAsync(provider);
        var id = await CreateAsync(provider, fixture, fixture.HouseholdAccountId, destinationId, 100m, 100m);
        Equal(baseline - 100m, await BalanceAsync(provider, fixture, fixture.HouseholdAccountId));
        Equal(100m, await BalanceAsync(provider, fixture, destinationId));

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await handler.GetAsync(fixture.HouseholdId, id, Ct);
            True(detail.Source != null && detail.Destination != null && detail.CanModify);
            await handler.UpdateAsync(fixture.HouseholdId, id, new(150m, 150m, Date, "corrected"), detail.UpdatedAt, Ct);
            await ThrowsAsync<ConflictException>(() => handler.UpdateAsync(fixture.HouseholdId, id,
                new(200m, 200m, Date, null), detail.UpdatedAt, Ct));
        }
        Equal(baseline - 150m, await BalanceAsync(provider, fixture, fixture.HouseholdAccountId));
        Equal(150m, await BalanceAsync(provider, fixture, destinationId));

        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            await ThrowsAsync<BusinessRuleException>(() => handler.CreateAsync(fixture.HouseholdId,
                destinationId, destinationId, new(10m, 10m, Date, null), Ct));
            await ThrowsAsync<BusinessRuleException>(() => handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, destinationId, new(10m, 9m, Date, null), Ct));
            await ThrowsAsync<ValidationException>(() => handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, destinationId, new(0m, 0m, Date, null), Ct));
            await ThrowsAsync<ValidationException>(() => handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, destinationId, new(1.00001m, 1.00001m, Date, null), Ct));
            await ThrowsAsync<BusinessRuleException>(() => handler.ListAsync(fixture.HouseholdId,
                new(null, null, null, 40, Date, null, null), Ct));
            await ThrowsAsync<NotFoundException>(() => handler.GetAsync(Guid.NewGuid(), id, Ct));
        }
        var fxTransfer = await CreateAsync(provider, fixture, fixture.HouseholdAccountId, fxId, 20m, 23.4567m);
        Equal(23.4567m, await BalanceAsync(provider, fixture, fxId));

        // Only the shared leg may be returned to a non-holder; guessing a private ID cannot filter it.
        var privateId = await CreateAsync(provider, fixture, fixture.HouseholdAccountId, fixture.PrivateAccountId, 30m, 30m);
        user.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await handler.GetAsync(fixture.HouseholdId, privateId, Ct);
            True(detail.Source != null && detail.Destination == null && detail.Note == null && !detail.CanModify);
            var page = await handler.ListAsync(fixture.HouseholdId, new(null, null, fixture.PrivateAccountId, 40, null, null, null), Ct);
            Equal(0, page.Items.Count);
            await ThrowsAsync<NotFoundException>(() => handler.VoidAsync(fixture.HouseholdId, privateId, detail.UpdatedAt, Ct));
            await ThrowsAsync<NotFoundException>(() => handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, fixture.PrivateAccountId, new(1m, 1m, Date, null), Ct));
            var balances = await scope.ServiceProvider.GetRequiredService<IAccountBalanceReader>()
                .GetCurrentBalancesAsync(fixture.HouseholdId, fixture.MemberId, [fixture.PrivateAccountId], Date, Ct);
            Equal(0, balances.Count);
        }
        user.UserId = fixture.OwnerUserId;

        // Before-anchor/future movements and pagination remain independent of the balance date.
        await CreateAsync(provider, fixture, destinationId, fxId, 7m, 8m, new DateOnly(2026, 8, 31));
        await CreateAsync(provider, fixture, destinationId, fxId, 9m, 10m, new DateOnly(2027, 1, 1));
        Equal(150m, await BalanceAsync(provider, fixture, destinationId));
        Equal(23.4567m, await BalanceAsync(provider, fixture, fxId));
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var seen = new HashSet<Guid>();
            TransferQuery query = new(null, null, null, 1, null, null, null);
            while (true)
            {
                var page = await handler.ListAsync(fixture.HouseholdId, query, Ct);
                foreach (var row in page.Items) True(seen.Add(row.Id));
                if (!page.HasMore) break;
                var last = page.Items.Last();
                query = query with { CursorDate = last.OccurredOn, CursorCreatedAt = last.CreatedAt, CursorId = last.Id };
            }
            Equal(5, seen.Count);
        }

        // Simultaneous edits of the same aggregate cannot overwrite one another.
        await using (var firstScope = provider.CreateAsyncScope())
        await using (var secondScope = provider.CreateAsyncScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var second = secondScope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var a = await first.Transfers.SingleAsync(t => t.Id == fxTransfer);
            var b = await second.Transfers.SingleAsync(t => t.Id == fxTransfer);
            a.Update(21m, 24m, Date, null, clock.GetUtcNow());
            b.Void(clock.GetUtcNow());
            await first.SaveChangesAsync();
            await ThrowsAsync<ConflictException>(() => secondScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct));
        }
        Equal(24m, await BalanceAsync(provider, fixture, fxId));

        // Commit an edit after the transfer SELECT starts but before its results are consumed.
        // Separate outgoing/incoming commands would mix old debits with new credits.
        var sourceBefore = await BalanceAsync(provider, fixture, fixture.HouseholdAccountId);
        var destinationBefore = await BalanceAsync(provider, fixture, destinationId);
        var interceptor = provider.GetRequiredService<TransferReadInterceptor>();
        interceptor.AfterTransferRead = async () =>
        {
            await using var writerScope = provider.CreateAsyncScope();
            var writer = writerScope.ServiceProvider.GetRequiredService<AverioDbContext>();
            await writer.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE transfers SET sent_amount = 175, received_amount = 175 WHERE id = {id}");
        };
        await using (var scope = provider.CreateAsyncScope())
        {
            var balances = await scope.ServiceProvider.GetRequiredService<IAccountBalanceReader>()
                .GetCurrentBalancesAsync(fixture.HouseholdId, fixture.OwnerMemberId,
                    [fixture.HouseholdAccountId, destinationId], Date, Ct);
            True(interceptor.AfterTransferRead == null);
            Equal(sourceBefore, balances[fixture.HouseholdAccountId]);
            Equal(destinationBefore, balances[destinationId]);
        }
        Equal(sourceBefore - 25m, await BalanceAsync(provider, fixture, fixture.HouseholdAccountId));
        Equal(destinationBefore + 25m, await BalanceAsync(provider, fixture, destinationId));
        await using (var scope = provider.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AverioDbContext>().Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE transfers SET sent_amount = 150, received_amount = 150 WHERE id = {id}");
        }
        Console.WriteLine("PASS transfer balance read stays consistent across a concurrent committed edit");

        // Archived accounts reject new transfers/edits but preserve the ability to void mistakes.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var account = await db.Accounts.SingleAsync(a => a.Id == destinationId);
            account.Archive(clock.GetUtcNow());
            await db.SaveChangesAsync();
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
            var detail = await handler.GetAsync(fixture.HouseholdId, id, Ct);
            await ThrowsAsync<ConflictException>(() => handler.UpdateAsync(fixture.HouseholdId, id,
                new(5m, 5m, Date, null), detail.UpdatedAt, Ct));
            await ThrowsAsync<ConflictException>(() => handler.CreateAsync(fixture.HouseholdId,
                fixture.HouseholdAccountId, destinationId, new(5m, 5m, Date, null), Ct));
            await handler.VoidAsync(fixture.HouseholdId, id, detail.UpdatedAt, Ct);
            await handler.VoidAsync(fixture.HouseholdId, id, detail.UpdatedAt, Ct);
            True((await handler.GetAsync(fixture.HouseholdId, id, Ct)).IsVoided);
        }
        Equal(0m, await BalanceAsync(provider, fixture, destinationId));
        Equal(aggregatesBefore, await AggregateFingerprintAsync(provider));

        // Database checks protect invariants even if a caller bypasses application validation.
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            var failure = await ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE transfers SET destination_account_id = source_account_id WHERE id = {id}"));
            Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
            failure = await ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE transfers SET received_amount = 1 WHERE id = {id}"));
            Equal(PostgresErrorCodes.CheckViolation, failure.SqlState);
        }
        Console.WriteLine("PASS transfers: paired balances, FX, privacy, pagination, validation, stale writes, archive, idempotent void, SQL constraints and unchanged cash flow");
    }

    private static async Task<Guid> CreateAsync(IServiceProvider provider, Fixture f, Guid source, Guid destination,
        decimal sent, decimal received, DateOnly? date = null)
    {
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TransferHandler>()
            .CreateAsync(f.HouseholdId, source, destination, new(sent, received, date ?? Date, "private note"), Ct);
    }

    private static async Task<decimal> BalanceAsync(IServiceProvider provider, Fixture f, Guid accountId)
    {
        await using var scope = provider.CreateAsyncScope();
        var balances = await scope.ServiceProvider.GetRequiredService<IAccountBalanceReader>()
            .GetCurrentBalancesAsync(f.HouseholdId, f.OwnerMemberId, [accountId], Date, Ct);
        return balances[accountId];
    }

    private static async Task<string> AggregateFingerprintAsync(IServiceProvider provider)
    {
        await using var scope = provider.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AverioDbContext>().TransactionAggregates.AsNoTracking().ToListAsync();
        return System.Text.Json.JsonSerializer.Serialize(rows.OrderBy(x => System.Text.Json.JsonSerializer.Serialize(x)));
    }

    private static void True(bool condition) { if (!condition) throw new Exception("Transfer assertion failed."); }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}."); }
    private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T e) { return e; } throw new Exception($"Expected {typeof(T).Name}."); }
}
