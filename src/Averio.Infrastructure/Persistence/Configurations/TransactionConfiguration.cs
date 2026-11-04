using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", table =>
        {
            table.HasCheckConstraint("ck_transactions_kind", "kind IN ('Income', 'Expense')");
            table.HasCheckConstraint("ck_transactions_visibility", "visibility IN ('Household', 'Private')");
            table.HasCheckConstraint("ck_transactions_status", "status IN ('Posted', 'Voided')");
            table.HasCheckConstraint("ck_transactions_amount", "amount > 0");
            table.HasCheckConstraint("ck_transactions_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.ImportBatchId).HasColumnName("import_batch_id");
        builder.HasOne<Averio.Domain.Imports.ImportBatch>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.ImportBatchId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.HouseholdId, x.ImportBatchId });
        builder.HasAlternateKey(x => new { x.HouseholdId, x.Id })
            .HasName("ak_transactions_household_id_id");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.CreatedByHouseholdMemberId).HasColumnName("created_by_household_member_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);
        builder.Property(x => x.Currency).HasColumnName("currency_code")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3).IsRequired();
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(160).IsRequired();
        builder.Property(x => x.Merchant).HasColumnName("merchant").HasMaxLength(160);
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();
        builder.Property(x => x.IsArchived).HasColumnName("is_archived").HasDefaultValue(false);
        builder.Property(x => x.VoidedAt).HasColumnName("voided_at");

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.AccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transactions_accounts_household_account");

        builder.HasOne<HouseholdMember>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.CreatedByHouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transactions_household_members_creator");

        builder.HasIndex(x => new { x.HouseholdId, x.OccurredOn, x.CreatedAt })
            .HasDatabaseName("ix_transactions_household_date_created");
        builder.HasIndex(x => new { x.HouseholdId, x.AccountId, x.OccurredOn })
            .HasDatabaseName("ix_transactions_household_account_date");
        builder.HasIndex(x => new { x.HouseholdId, x.Status, x.OccurredOn })
            .HasDatabaseName("ix_transactions_household_status_date");
        builder.HasIndex(x => new { x.HouseholdId, x.Visibility, x.OccurredOn })
            .HasDatabaseName("ix_transactions_household_visibility_date");
    }
}
