using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Labels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class LabelConfiguration
    : IEntityTypeConfiguration<Label>
{
    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable("labels", table =>
        {
            table.HasCheckConstraint(
                "ck_labels_scope",
                "scope IN ('Household', 'Account')");
            table.HasCheckConstraint(
                "ck_labels_scope_account",
                "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.HouseholdId, x.Id })
            .HasName("ak_labels_household_id_id");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(40).IsRequired();
        builder.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(40).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");

        builder.HasOne<Household>().WithMany()
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_labels_households");

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.AccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_labels_accounts");

        builder.HasIndex(x => new { x.HouseholdId, x.NormalizedName })
            .IsUnique()
            .HasFilter("scope = 'Household'")
            .HasDatabaseName("ux_labels_household_name");

        builder.HasIndex(x => new { x.HouseholdId, x.AccountId, x.NormalizedName })
            .IsUnique()
            .HasFilter("scope = 'Account'")
            .HasDatabaseName("ux_labels_account_name");

        builder.HasIndex(x => new { x.HouseholdId, x.Scope, x.AccountId })
            .HasDatabaseName("ix_labels_scope_account");
    }
}
