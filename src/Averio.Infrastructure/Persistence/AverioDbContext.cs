using Averio.Domain.Households;
using Averio.Domain.Accounts;
using Averio.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence;

public sealed class AverioDbContext(
    DbContextOptions<AverioDbContext> options): IdentityUserContext<ApplicationUser, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Household> Households => Set<Household>();
    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();
    public DbSet<DataProtectionKey> DataProtectionKeys
    {
        get;
        set;
    } = null!;
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AverioDbContext).Assembly);
    }
}