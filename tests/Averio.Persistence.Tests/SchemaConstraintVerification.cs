using Averio.Domain.Accounts;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class SchemaConstraintVerification
{
    public static async Task RunAsync(IServiceProvider provider, Fixture fixture, DateTimeOffset now)
    {
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AverioDbContext>();

        await RejectConstraintAsync(
            () => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO households (id, version, created_at_utc, onboarding_completed_at_utc)
                VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {now}, {now});
                """),
            "ck_households_completed_onboarding_profile");

        await RejectConstraintAsync(
            () => db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO households
                    (id, version, name, country_code, preferred_currency, created_at_utc, onboarding_completed_at_utc)
                VALUES
                    ({Guid.NewGuid()}, {Guid.NewGuid()}, {"  "}, {"DE"}, {"EUR"}, {now}, {now});
                """),
            "ck_households_completed_onboarding_profile");

        await RejectConstraintAsync(
            () => db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE accounts
                SET status = 'Archived', archived_at = NULL
                WHERE id = {fixture.HouseholdAccountId};
                """),
            "ck_accounts_archived_at_matches_status");

            await RejectConstraintAsync(
                () => db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE accounts
                    SET archived_at = {now}
                    WHERE id = {fixture.HouseholdAccountId};
                    """),
                "ck_accounts_archived_at_matches_status");

        var account = await db.Accounts.SingleAsync(x => x.Id == fixture.HouseholdAccountId);
        account.Archive(now);
        await db.SaveChangesAsync();
        account.Restore(now.AddSeconds(1));
        await db.SaveChangesAsync();

        Console.WriteLine("PASS household onboarding and account archive database constraints");
    }

    private static async Task RejectConstraintAsync(Func<Task<int>> operation, string expectedConstraint)
    {
        try
        {
            await operation();
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.CheckViolation
            && exception.ConstraintName == expectedConstraint)
        {
            return;
        }

        throw new InvalidOperationException($"Expected check constraint {expectedConstraint} to reject the write.");
    }
}
