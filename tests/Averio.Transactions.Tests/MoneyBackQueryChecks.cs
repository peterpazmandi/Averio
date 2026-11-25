using System.Data.Common;
using Averio.Application.Recurring;
using Averio.Infrastructure;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

internal static class MoneyBackQueryChecks
{
    public static async Task RunAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:AverioDatabase"] = "Host=127.0.0.1;Port=1;Database=unused;Username=unused",
        }).Build());
        services.ConfigureDbContext<AverioDbContext>(options => options.AddInterceptors(new StopBeforeConnect()), ServiceLifetime.Singleton);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IMoneyBackRepository>();
        foreach (var status in new string?[] { null, "Received", "Cancelled", "Submitted" })
        {
            try
            {
                await repository.ListAsync(Guid.NewGuid(), Guid.NewGuid(), 25, 26, status, CancellationToken.None);
                throw new Exception("Expected the connection interceptor.");
            }
            catch (QueryTranslatedException) { }
        }
    }

    private sealed class QueryTranslatedException : Exception;
    private sealed class StopBeforeConnect : DbConnectionInterceptor
    {
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) => throw new QueryTranslatedException();
    }
}
