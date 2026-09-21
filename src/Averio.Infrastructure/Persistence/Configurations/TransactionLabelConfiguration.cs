using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class TransactionLabelConfiguration
    : IEntityTypeConfiguration<TransactionLabel>
{
    public void Configure(EntityTypeBuilder<TransactionLabel> builder)
    {
        builder.ToTable("transaction_labels", table =>
        {
            table.HasCheckConstraint("ck_transaction_labels_scope", "scope IN ('Household', 'Account')");
            table.HasCheckConstraint(
                "ck_transaction_labels_scope_account",
                "(scope = 'Household' AND account_id IS NULL) OR (scope = 'Account' AND account_id IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(40).IsRequired();

        builder.HasOne<Transaction>()
            .WithMany(x => x.Labels)
            .HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_transaction_labels_transactions");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.AccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_labels_accounts");

        builder.HasIndex(x => new { x.HouseholdId, x.TransactionId })
            .HasDatabaseName("ix_transaction_labels_transaction");
        builder.HasIndex(x => new { x.HouseholdId, x.AccountId })
            .HasDatabaseName("ix_transaction_labels_account");
    }
}
