using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("transfers", table =>
        {
            table.HasCheckConstraint("ck_transfers_distinct_accounts", "source_account_id <> destination_account_id");
            table.HasCheckConstraint("ck_transfers_amounts", "sent_amount > 0 AND received_amount > 0");
            table.HasCheckConstraint("ck_transfers_currencies", "source_currency IN ('EUR','USD','HUF') AND destination_currency IN ('EUR','USD','HUF')");
            table.HasCheckConstraint("ck_transfers_same_currency", "source_currency <> destination_currency OR sent_amount = received_amount");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.SourceAccountId).HasColumnName("source_account_id");
        builder.Property(x => x.DestinationAccountId).HasColumnName("destination_account_id");
        builder.Property(x => x.CreatedByHouseholdMemberId).HasColumnName("created_by_household_member_id");
        builder.Property(x => x.SourceCurrency).HasColumnName("source_currency")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3).IsRequired();
        builder.Property(x => x.DestinationCurrency).HasColumnName("destination_currency")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3).IsRequired();
        builder.Property(x => x.SentAmount).HasColumnName("sent_amount").HasPrecision(19, 4);
        builder.Property(x => x.ReceivedAmount).HasColumnName("received_amount").HasPrecision(19, 4);
        builder.Property(x => x.OccurredOn).HasColumnName("occurred_on");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();
        builder.Property(x => x.VoidedAt).HasColumnName("voided_at");
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.SourceAccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.DestinationAccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<HouseholdMember>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.CreatedByHouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.HouseholdId, x.OccurredOn, x.CreatedAt, x.Id });
        builder.HasIndex(x => new { x.HouseholdId, x.SourceAccountId, x.OccurredOn });
        builder.HasIndex(x => new { x.HouseholdId, x.DestinationAccountId, x.OccurredOn });
    }
}
