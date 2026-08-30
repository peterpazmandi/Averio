using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence;

public sealed class AverioDbContext(
    DbContextOptions<AverioDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AverioDbContext).Assembly);
    }
}