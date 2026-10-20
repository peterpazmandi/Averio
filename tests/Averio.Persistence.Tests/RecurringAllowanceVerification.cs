using Averio.Application.Common.Exceptions;
using Averio.Application.PersonalMoney;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

internal static class RecurringAllowanceVerification
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    public static async Task RunAsync(IServiceProvider provider, MutableCurrentUser user,
        Fixture fixture, MutableTimeProvider clock)
    {
        user.UserId = fixture.OwnerUserId;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var starts = today.AddDays(1);
        Guid id;
        var operation = Guid.NewGuid();
        decimal beforeBalance;
        int transactionsBefore, transfersBefore;
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            transactionsBefore = await db.Transactions.CountAsync(Ct);
            transfersBefore = await db.Transfers.CountAsync(Ct);
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            beforeBalance = (await handler.GetMineAsync(fixture.HouseholdId, Ct))?.Available ?? 0m;
            var schedules = scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>();
            var input = new RecurringAllowanceInput(fixture.OwnerMemberId, "EUR",
                220m, starts, null, operation);
            id = await schedules.CreateAsync(fixture.HouseholdId, input, Ct);
            var retried = await schedules.CreateAsync(fixture.HouseholdId, input, Ct);
            if (retried != id) throw new Exception("Retry created duplicate schedule.");
            await ThrowsAsync<ConflictException>(() => schedules.CreateAsync(fixture.HouseholdId,
                input with { Amount = 221m }, Ct));
        }

        user.UserId = fixture.MemberUserId;
        await using (var scope = provider.CreateAsyncScope())
        {
            var schedules = scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>();
            if ((await schedules.ListAsync(fixture.HouseholdId, Ct)).Any(x => x.Id == id))
                throw new Exception("Other members must not see personal scheduled grants.");
            await ThrowsAsync<ForbiddenException>(() => schedules.CreateAsync(fixture.HouseholdId,
                new RecurringAllowanceInput(fixture.MemberId, "EUR", 50m,
                    starts, null, Guid.NewGuid()), Ct));
            await ThrowsAsync<NotFoundException>(() =>
                schedules.HistoryAsync(fixture.HouseholdId, id, Ct));
        }
        user.UserId = fixture.OwnerUserId;
        RecurringAllowanceCandidate candidate;
        await using (var scope = provider.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>();
            candidate = (await repository.DueAsync(starts, 100, Ct)).Single(x => x.RuleId == id);
            if (await repository.HasReceiptAsync(id, starts, Ct))
                throw new Exception("Future allowance granted before due.");
        }
        clock.Advance(TimeSpan.FromDays(1));
        await using (var scope = provider.CreateAsyncScope())
        {
            var schedules = scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>();
            if (!await schedules.ProcessAsync(candidate, Ct))
                throw new Exception("First monthly allowance not processed.");
            if (await schedules.ProcessAsync(candidate, Ct))
                throw new Exception("Stale worker candidate processed twice.");
            var current = (await schedules.ListAsync(fixture.HouseholdId, Ct)).Single(x => x.Id == id);
            if (current.NextDueOn != starts.AddMonths(1))
                throw new Exception("Next occurrence date did not advance.");
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var wallet = await handler.GetMineAsync(fixture.HouseholdId, Ct);
            if (wallet?.Available != beforeBalance + 220m)
                throw new Exception("Recurring grant failed to increase entitlement.");
            if (wallet.Entries.Count(x => x.RecurringAllowanceRuleId == id) != 1)
                throw new Exception("Due occurrence must have exactly one grant receipt.");
            var repository = scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>();
            var history = await repository.HistoryAsync(fixture.HouseholdId, id, Ct);
            if (history.Count != 1 || history[0].Amount != 220m)
                throw new Exception("Recurring grant history is inaccurate.");
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var schedules = scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>();
            var current = (await schedules.ListAsync(fixture.HouseholdId, Ct)).Single(x => x.Id == id);
            await schedules.UpdateAsync(fixture.HouseholdId, id,
                new RecurringAllowanceUpdate(240m, null, true, false, current.Version), Ct);
        }
        clock.Advance(TimeSpan.FromDays(35));
        await using (var scope = provider.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>();
            if ((await repo.DueAsync(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), 100, Ct))
                .Any(x => x.RuleId == id))
                throw new Exception("Paused schedule was returned as due.");
            var schedules = scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>();
            var paused = (await schedules.ListAsync(fixture.HouseholdId, Ct)).Single(x => x.Id == id);
            await schedules.UpdateAsync(fixture.HouseholdId, id,
                new RecurringAllowanceUpdate(240m, null, false, false, paused.Version), Ct);
            await ThrowsAsync<ConflictException>(() => schedules.UpdateAsync(fixture.HouseholdId,
                id, new RecurringAllowanceUpdate(240m, null, false, false, paused.Version), Ct));
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRecurringAllowanceRepository>();
            var newCandidate = (await repo.DueAsync(DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), 100, Ct))
                .Single(x => x.RuleId == id);
            if (newCandidate.DueOn != starts.AddMonths(1))
                throw new Exception("Pause/resume lost the missed monthly occurrence.");
            var schedules = scope.ServiceProvider.GetRequiredService<RecurringAllowanceHandler>();
            if (!await schedules.ProcessAsync(newCandidate, Ct))
                throw new Exception("Resumed allowance failed to catch up.");
            var current = (await schedules.ListAsync(fixture.HouseholdId, Ct)).Single(x => x.Id == id);
            await schedules.UpdateAsync(fixture.HouseholdId, id,
                new RecurringAllowanceUpdate(240m, null, false, true, current.Version), Ct);
            if (await repo.HasReceiptAsync(id, starts.AddMonths(2), Ct))
                throw new Exception("Stop created a future grant.");
        }
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();
            if (await db.Transactions.CountAsync(Ct) != transactionsBefore
                || await db.Transfers.CountAsync(Ct) != transfersBefore)
                throw new Exception("Recurring entitlement modified the bank ledger.");
            var handler = scope.ServiceProvider.GetRequiredService<PersonalMoneyHandler>();
            var wallet = await handler.GetMineAsync(fixture.HouseholdId, Ct);
            if (wallet?.Available != beforeBalance + 460m)
                throw new Exception("Monthly rollover/amount update changed historical grant.");
            if (wallet.Entries.Count(x => x.RecurringAllowanceRuleId == id) != 2)
                throw new Exception("Two monthly due dates must have two unique grants.");
        }
        Console.WriteLine("PASS monthly Personal Money: owner, privacy, retry, pause, catch-up, stop, no bank postings");
    }

    private static async Task ThrowsAsync<T>(Func<Task> callback) where T : Exception
    {
        try { await callback(); }
        catch (T) { return; }
        throw new Exception($"Expected {typeof(T).Name}.");
    }
}
