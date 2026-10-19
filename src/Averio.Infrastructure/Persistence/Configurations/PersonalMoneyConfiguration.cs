using Averio.Domain.Households;
using Averio.Domain.PersonalMoney;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class PersonalMoneyWalletConfiguration : IEntityTypeConfiguration<PersonalMoneyWallet>
{
    public void Configure(EntityTypeBuilder<PersonalMoneyWallet> b)
    {
        b.ToTable("personal_money_wallets", t =>
        {
            t.HasCheckConstraint("ck_personal_money_wallet_currency", "currency IN ('EUR','USD','HUF')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.HouseholdId, x.Id })
            .HasName("ak_personal_money_wallet_household_id_id");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.Currency).HasColumnName("currency")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId }).IsUnique();
        b.HasOne<HouseholdMember>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PersonalMoneyEntryConfiguration : IEntityTypeConfiguration<PersonalMoneyEntry>
{
    public void Configure(EntityTypeBuilder<PersonalMoneyEntry> b)
    {
        b.ToTable("personal_money_entries", t =>
        {
            t.HasCheckConstraint("ck_personal_money_entry_amount", "amount <> 0");
            t.HasCheckConstraint("ck_personal_money_entry_kind",
                "kind IN ('Grant','Funding','PrivateCharge')");
            t.HasCheckConstraint("ck_personal_money_entry_source",
                "(transfer_id IS NULL OR transaction_id IS NULL) AND " +
                "(kind <> 'Funding' OR transfer_id IS NOT NULL) AND " +
                "(kind <> 'PrivateCharge' OR transaction_id IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.WalletId).HasColumnName("wallet_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);
        b.Property(x => x.OccurredOn).HasColumnName("occurred_on");
        b.Property(x => x.TransferId).HasColumnName("transfer_id");
        b.Property(x => x.TransactionId).HasColumnName("transaction_id");
        b.Property(x => x.ReversesEntryId).HasColumnName("reverses_entry_id");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.CorrectionExpectedAmount).HasColumnName("correction_expected_amount").HasPrecision(19, 4);
        b.Property(x => x.CorrectionExpectedVersion).HasColumnName("correction_expected_version");
        b.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        b.HasOne<PersonalMoneyWallet>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.WalletId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transfer>().WithMany()
            .HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne<PersonalMoneyEntry>().WithMany()
            .HasForeignKey(x => x.ReversesEntryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.WalletId, x.OccurredOn, x.RecordedAt });
        b.HasIndex(x => new { x.HouseholdId, x.TransferId });
        b.HasIndex(x => new { x.HouseholdId, x.TransactionId });
        b.HasIndex(x => x.ReversesEntryId).IsUnique();
        b.HasIndex(x => new { x.WalletId, x.OperationId }).IsUnique();
    }
}
