using System.Data.Common;
using Averio.Application.Recurring;
using Averio.Infrastructure;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

internal static class MoneyBackSnapshotVerification
{
    public static async Task RunAsync(IServiceProvider provider, Fixture fixture, Guid claimId)
    {
        await using var scope = provider.CreateAsyncScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<AverioDbContext>().Database.GetConnectionString()
            ?? throw new InvalidOperationException("Test database connection is missing.");
        var interceptor = new ChangeStatusAfterKeys(connectionString, claimId);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AverioDatabase"] = connectionString,
        }).Build());
        services.ConfigureDbContext<AverioDbContext>(options => options.AddInterceptors(interceptor), ServiceLifetime.Singleton);
        await using var testProvider = services.BuildServiceProvider();
        try
        {
            await using var testScope = testProvider.CreateAsyncScope();
            var repository = testScope.ServiceProvider.GetRequiredService<IMoneyBackRepository>();
            var rows = await repository.ListAsync(fixture.HouseholdId, fixture.OwnerMemberId, 0, 100, "NeedsSubmission", CancellationToken.None);
            if (!interceptor.Changed || rows.All(row => row.Id != claimId || row.Status != "NeedsSubmission"))
                throw new Exception("Money-back keys and details did not use the same snapshot.");
        }
        finally
        {
            if (interceptor.Changed) await SetStatus(connectionString, claimId, "NeedsSubmission");
        }
    }

    private static async Task SetStatus(string connectionString, Guid claimId, string status)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE reimbursement_claims SET status = @status WHERE id = @id", connection);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("id", claimId);
        if (await command.ExecuteNonQueryAsync() != 1) throw new Exception("Snapshot fixture claim was not updated.");
    }

    private sealed class ChangeStatusAfterKeys(string connectionString, Guid claimId) : DbCommandInterceptor
    {
        public bool Changed { get; private set; }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (!Changed && command.CommandText.Contains("UNION ALL", StringComparison.OrdinalIgnoreCase))
            {
                await SetStatus(connectionString, claimId, "Cancelled");
                Changed = true;
            }
            return result;
        }
    }
}
