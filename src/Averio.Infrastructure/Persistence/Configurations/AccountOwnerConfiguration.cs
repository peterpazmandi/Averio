using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class AccountOwnerConfiguration : IEntityTypeConfiguration<AccountOwner>
{
    public void Configure(EntityTypeBuilder<AccountOwner> builder)
    {
        builder.ToTable("account_owners");
        builder.HasKey(x => new { x.AccountId, x.HouseholdMemberId })
            .HasName("pk_account_owners");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");

        builder.HasOne<Account>().WithMany(x => x.Owners)
            .HasForeignKey(x => new { x.HouseholdId, x.AccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_account_owners_accounts");

        builder.HasOne<HouseholdMember>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_account_owners_members");

        builder.HasIndex(x => new { x.HouseholdId, x.AccountId })
            .HasDatabaseName("ix_account_owners_household_account");
        builder.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasDatabaseName("ix_account_owners_household_member");
    }
}
