using Averio.Domain.Accounts;
using Averio.Domain.Common;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable(
            "accounts",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_accounts_currency",
                    """
                    currency_code IN ('EUR', 'USD', 'HUF')
                    """);

                table.HasCheckConstraint(
                    "ck_accounts_ownership_visibility",
                    """
                    (
                        (
                            ownership = 'Household'
                            AND owner_household_member_id IS NULL
                            AND visibility = 'Household'
                            AND include_in_household_reports = true
                        )
                        OR
                        (
                            ownership = 'Personal'
                            AND owner_household_member_id IS NOT NULL
                            AND (
                                visibility <> 'Private'
                                OR include_in_household_reports = false
                            )
                        )
                    )
                    """);
            });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.HouseholdId)
            .HasColumnName("household_id");

        builder.Property(x => x.OwnerHouseholdMemberId)
            .HasColumnName("owner_household_member_id");

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Ownership)
            .HasColumnName("ownership")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Visibility)
            .HasColumnName("visibility")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.IncludeInHouseholdReports)
            .HasColumnName("include_in_household_reports");

        builder.Property(x => x.Currency)
            .HasColumnName("currency_code")
            .HasConversion(
                value => value.Value,
                value => CurrencyCode.From(value))
            .HasMaxLength(3);

        builder.Property(x => x.BalanceAnchorAmount)
            .HasColumnName("balance_anchor_amount")
            .HasPrecision(19, 4);

        builder.Property(x => x.BalanceAnchorDate)
            .HasColumnName("balance_anchor_date");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(x => x.ArchivedAt)
            .HasColumnName("archived_at");

        builder.HasIndex(x => x.HouseholdId);

        builder.HasIndex(x => x.OwnerHouseholdMemberId);

        builder.HasOne<HouseholdMember>()
        .WithMany()
        .HasForeignKey(account => new
        {
            account.HouseholdId,
            account.OwnerHouseholdMemberId
        })
        .HasPrincipalKey(member => new
        {
            member.HouseholdId,
            member.Id
        })
        .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(
            x => new
            {
                x.HouseholdId,
                x.Status,
            });

        builder.HasIndex(
            x => new
            {
                x.HouseholdId,
                x.Visibility,
            });
    }
}