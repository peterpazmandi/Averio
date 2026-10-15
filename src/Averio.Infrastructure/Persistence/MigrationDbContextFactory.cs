using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Averio.Infrastructure.Persistence;

/// <summary>Migration tooling must never inherit an application's shared database secrets.</summary>
public sealed class MigrationDbContextFactory : IDesignTimeDbContextFactory<AverioDbContext>
{
    public AverioDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("AVERIO_MIGRATION_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Set AVERIO_MIGRATION_CONNECTION_STRING explicitly for EF tooling. For scaffolding, use a local placeholder; never rely on application user secrets.");
        return new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>().UseNpgsql(connection).Options);
    }
}
