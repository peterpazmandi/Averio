using Averio.Application.Common.Exceptions;
using Averio.Application.Transfers;
using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class TransferAccountConcurrencyVerification
{
    private static readonly DateOnly Date = new(2026, 9, 21);
    private static readonly CancellationToken Ct = CancellationToken.None;

    public static async Task RunAsync(IServiceProvider provider, Fixture fixture, TimeProvider clock)
    {
        foreach (var operation in new[] { "create", "update", "void" })
        {
            foreach (var removeOwner in new[] { false, true })
                await AccountChangeWinsAsync(provider, fixture, clock, operation, removeOwner);
            await TransferWinsAsync(provider, fixture, clock, operation);
        }
        Console.WriteLine("PASS transfer account concurrency: create/update/void versus archival and ownership removal, both commit orders");
    }

    private static async Task<(Guid Source, Guid Destination, Guid? Transfer, DateTimeOffset Version)> SeedAsync(
        IServiceProvider provider, Fixture f, TimeProvider clock, string operation)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
        var source = Account.Create(f.HouseholdId, [f.OwnerMemberId, f.MemberId], "Concurrency source",
            AccountType.Checking, AccountScope.Personal, AccountVisibility.Private, false,
            CurrencyCode.Eur, 100m, Date, clock.GetUtcNow());
        var destination = Account.Create(f.HouseholdId, [], "Concurrency destination",
            AccountType.Savings, AccountScope.Household, AccountVisibility.Household, true,
            CurrencyCode.Eur, 0m, Date, clock.GetUtcNow());
        db.Accounts.AddRange(source, destination);
        await db.SaveChangesAsync();
        if (operation == "create") return (source.Id, destination.Id, null, default);
        var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
        var id = await handler.CreateAsync(f.HouseholdId, source.Id, destination.Id, new(10m, 10m, Date, null), Ct);
        var detail = await handler.GetAsync(f.HouseholdId, id, Ct);
        return (source.Id, destination.Id, id, detail.UpdatedAt);
    }

    private static async Task RunOperationAsync(IServiceProvider provider, Fixture f, string operation,
        (Guid Source, Guid Destination, Guid? Transfer, DateTimeOffset Version) seed)
    {
        await using var scope = provider.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<TransferHandler>();
        if (operation == "create")
            await handler.CreateAsync(f.HouseholdId, seed.Source, seed.Destination, new(20m, 20m, Date, null), Ct);
        else if (operation == "update")
            await handler.UpdateAsync(f.HouseholdId, seed.Transfer!.Value, new(20m, 20m, Date, null), seed.Version, Ct);
        else
            await handler.VoidAsync(f.HouseholdId, seed.Transfer!.Value, seed.Version, Ct);
    }

    private static async Task ChangeAccountAsync(AverioDbContext db, Fixture f, TimeProvider clock,
        Guid id, bool removeOwner)
    {
        var account = await db.Accounts.Include(a => a.Owners).SingleAsync(a => a.Id == id);
        if (removeOwner)
            account.Update(account.Name, account.Type, [f.MemberId], account.Visibility,
                account.IncludeInHouseholdReports, clock.GetUtcNow());
        else
            account.Archive(clock.GetUtcNow());
        // Uses the same aggregate and atomic EF persistence as account handlers.
        await db.SaveChangesAsync();
    }

    private static async Task AccountChangeWinsAsync(IServiceProvider provider, Fixture f, TimeProvider clock,
        string operation, bool removeOwner)
    {
        var seed = await SeedAsync(provider, f, clock, operation);
        var interceptor = provider.GetRequiredService<TransferReadInterceptor>();
        await using var writerScope = provider.CreateAsyncScope();
        var writer = writerScope.ServiceProvider.GetRequiredService<AverioDbContext>();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await ChangeAccountAsync(writer, f, clock, seed.Source, removeOwner);
        var attempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        interceptor.BeforeAccountLock = () => { attempted.TrySetResult(); return Task.CompletedTask; };
        var pending = RunOperationAsync(provider, f, operation, seed);
        try
        {
            await attempted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await transaction.CommitAsync();
            if (removeOwner)
                await ThrowsAsync<NotFoundException>(() => pending);
            else if (operation != "void")
                await ThrowsAsync<ConflictException>(() => pending);
            else
                await pending; // Archival intentionally still permits voiding.
        }
        finally
        {
            interceptor.BeforeAccountLock = null;
            await transaction.DisposeAsync();
        }
        await using var checkScope = provider.CreateAsyncScope();
        var db = checkScope.ServiceProvider.GetRequiredService<AverioDbContext>();
        var rows = await db.Transfers.Where(t => t.SourceAccountId == seed.Source).ToListAsync();
        if (operation == "create")
            Check(rows.Count == 0, "A rejected create must not write a transfer.");
        else
        {
            Check(rows.Single().SentAmount == 10m, "A rejected update must preserve amounts.");
            Check((rows.Single().VoidedAt != null) == (!removeOwner && operation == "void"),
                "Only the authorized archive/void scenario may void the transfer.");
        }
    }

    private static async Task TransferWinsAsync(IServiceProvider provider, Fixture f, TimeProvider clock, string operation)
    {
        var seed = await SeedAsync(provider, f, clock, operation);
        var interceptor = provider.GetRequiredService<TransferReadInterceptor>();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        interceptor.AfterAccountLock = async () =>
        {
            locked.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(15));
        };
        var pending = RunOperationAsync(provider, f, operation, seed);
        try
        {
            await locked.Task.WaitAsync(TimeSpan.FromSeconds(10));
            // Both account rows must block edits; owners and status cannot commit halfway through.
            foreach (var accountId in new[] { seed.Source, seed.Destination })
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
                await using var transaction = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '500ms'");
                var error = await ThrowsAsync<Exception>(() =>
                    ChangeAccountAsync(db, f, clock, accountId, accountId == seed.Source));
                Check(error.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.LockNotAvailable },
                    "Account edit must wait for the transfer's row lock.");
                await transaction.RollbackAsync();
            }
        }
        finally
        {
            interceptor.AfterAccountLock = null;
            release.TrySetResult();
        }
        await pending.WaitAsync(TimeSpan.FromSeconds(10));
        // Locks are released after commit, so a subsequent authorized account edit succeeds.
        await using var afterScope = provider.CreateAsyncScope();
        await ChangeAccountAsync(afterScope.ServiceProvider.GetRequiredService<AverioDbContext>(), f, clock, seed.Source, true);
    }

    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T error) { return error; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
