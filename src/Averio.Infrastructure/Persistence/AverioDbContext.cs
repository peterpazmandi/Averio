using Averio.Domain.Households;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
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
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<TransactionLabel> TransactionLabels => Set<TransactionLabel>();
    public DbSet<TransactionAggregate> TransactionAggregates => Set<TransactionAggregate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AverioDbContext).Assembly);
    }
}
