using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
namespace Averio.Infrastructure.Persistence;

/// <summary>Migration tooling must never inherit an application's shared database secrets.</summary>
public sealed class MigrationDbContextFactory : IDesignTimeDbContextFactory<AverioDbContext>
{
    private static readonly IServiceProvider IdentityServices = CreateIdentityServices();

    public AverioDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("AVERIO_MIGRATION_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set AVERIO_MIGRATION_CONNECTION_STRING explicitly for EF tooling. For scaffolding, use a local placeholder; never rely on application user secrets.");
        // The Identity passkey entity exists only in schema version 3. EF
        // tooling must build the same model as the runtime service provider.
        return new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>()
            .UseNpgsql(connection).UseApplicationServiceProvider(IdentityServices).Options);
    }

    private static IServiceProvider CreateIdentityServices()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<IdentityOptions>(options =>
        {
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            options.Stores.MaxLengthForKeys = 128;
        });
        return services.BuildServiceProvider();
    }
}
