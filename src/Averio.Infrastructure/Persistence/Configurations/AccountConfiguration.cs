using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts", table =>
        {
            table.HasCheckConstraint("ck_accounts_currency", "currency_code IN ('EUR', 'USD', 'HUF')");
            table.HasCheckConstraint("ck_accounts_scope", "scope IN ('Household', 'Personal')");
            table.HasCheckConstraint("ck_accounts_visibility", "visibility IN ('Household', 'Private')");
            table.HasCheckConstraint("ck_accounts_type", "type IN ('Checking', 'Savings', 'Cash', 'CreditCard')");
            table.HasCheckConstraint("ck_accounts_status", "status IN ('Active', 'Archived')");
            table.HasCheckConstraint("ck_accounts_scope_visibility", """
                (scope = 'Household' AND visibility = 'Household' AND include_in_household_reports)
                OR
                (scope = 'Personal' AND (visibility <> 'Private' OR NOT include_in_household_reports))
                """);
        });

        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.HouseholdId, x.Id })
            .HasName("ak_accounts_household_id_id");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.IncludeInHouseholdReports).HasColumnName("include_in_household_reports");
        builder.Property(x => x.Currency).HasColumnName("currency_code")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3).IsRequired();
        builder.Property(x => x.BalanceAnchorAmount).HasColumnName("balance_anchor_amount").HasPrecision(19, 4);
        builder.Property(x => x.BalanceAnchorDate).HasColumnName("balance_anchor_date");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();
        builder.Property(x => x.ArchivedAt).HasColumnName("archived_at");

        builder.HasOne<Household>().WithMany()
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_accounts_households_household_id");

        builder.Navigation(x => x.Owners).HasField("_owners")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => x.HouseholdId);
        builder.HasIndex(x => new { x.HouseholdId, x.Status });
        builder.HasIndex(x => new { x.HouseholdId, x.Visibility });
    }
}
