using Averio.Domain.Households;
using Averio.Domain.Accounts;
using Averio.Domain.Categories;
using Averio.Domain.Labels;
using Averio.Domain.Transactions;
using Averio.Domain.PersonalMoney;
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
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<IconCatalogEntry> Icons => Set<IconCatalogEntry>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<Averio.Domain.Transfers.Transfer> Transfers => Set<Averio.Domain.Transfers.Transfer>();
    public DbSet<PersonalMoneyWallet> PersonalMoneyWallets => Set<PersonalMoneyWallet>();
    public DbSet<PersonalMoneyEntry> PersonalMoneyEntries => Set<PersonalMoneyEntry>();
    public DbSet<RecurringAllowanceRule> RecurringAllowanceRules => Set<RecurringAllowanceRule>();
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
