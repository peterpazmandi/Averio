using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using Averio.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class TransactionAggregateConfiguration
    : IEntityTypeConfiguration<TransactionAggregate>
{
    public void Configure(EntityTypeBuilder<TransactionAggregate> builder)
    {
        builder.ToTable("transaction_aggregates", table =>
        {
            table.HasCheckConstraint(
                "ck_transaction_aggregates_dimension",
                "dimension_type IN ('Account', 'Category')");
            table.HasCheckConstraint(
                "ck_transaction_aggregates_period",
                "period IN ('Day', 'Month', 'Year')");
            table.HasCheckConstraint(
                "ck_transaction_aggregates_currency",
                "currency_code IN ('EUR', 'USD', 'HUF')");
            table.HasCheckConstraint(
                "ck_transaction_aggregates_non_negative",
                "income_amount >= 0 AND expense_amount >= 0 AND entry_count >= 0");
        });

        builder.HasKey(x => new
        {
            x.HouseholdId,
            x.AccountId,
            x.DimensionType,
            x.DimensionId,
            x.Currency,
            x.Period,
            x.PeriodStart,
        });

        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.AccountId).HasColumnName("account_id");
        builder.Property(x => x.DimensionType).HasColumnName("dimension_type")
            .HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.DimensionId).HasColumnName("dimension_id");
        builder.Property(x => x.Currency).HasColumnName("currency_code")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3);
        builder.Property(x => x.Period).HasColumnName("period")
            .HasConversion<string>().HasMaxLength(10);
        builder.Property(x => x.PeriodStart).HasColumnName("period_start");
        builder.Property(x => x.IncomeAmount).HasColumnName("income_amount").HasPrecision(19, 4);
        builder.Property(x => x.ExpenseAmount).HasColumnName("expense_amount").HasPrecision(19, 4);
        builder.Property(x => x.EntryCount).HasColumnName("entry_count");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.AccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_aggregates_accounts_household_account");

        builder.HasIndex(x => new
        {
            x.HouseholdId,
            x.DimensionType,
            x.Period,
            x.PeriodStart,
            x.Currency,
        }).HasDatabaseName("ix_transaction_aggregates_household_dimension_period");

        builder.HasIndex(x => new { x.HouseholdId, x.DimensionType, x.DimensionId })
            .HasDatabaseName("ix_transaction_aggregates_dimension");
    }
}
