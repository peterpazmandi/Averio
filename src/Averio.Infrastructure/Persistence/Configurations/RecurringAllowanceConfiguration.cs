using Averio.Domain.Households;
using Averio.Domain.PersonalMoney;
using Averio.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class RecurringAllowanceConfiguration : IEntityTypeConfiguration<RecurringAllowanceRule>
{
    public void Configure(EntityTypeBuilder<RecurringAllowanceRule> b)
    {
        b.ToTable("recurring_allowance_rules", table =>
        {
            table.HasCheckConstraint("ck_recurring_allowance_amount", "amount > 0");
            table.HasCheckConstraint("ck_recurring_allowance_dates",
                "(ends_on IS NULL OR ends_on >= starts_on) AND occurrence_index >= 0 " +
                "AND (next_due_on IS NULL OR next_due_on >= starts_on)");
            table.HasCheckConstraint("ck_recurring_allowance_currency", "currency IN ('EUR','USD','HUF')");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.HouseholdId, x.Id });
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CreationOperationId).HasColumnName("creation_operation_id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.RecipientMemberId).HasColumnName("recipient_member_id");
        b.Property(x => x.Currency).HasColumnName("currency")
            .HasConversion<CurrencyCodeConverter>().HasMaxLength(3);
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);
        b.Property(x => x.StartsOn).HasColumnName("starts_on");
        b.Property(x => x.EndsOn).HasColumnName("ends_on");
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index");
        b.Property(x => x.NextDueOn).HasColumnName("next_due_on");
        b.Property(x => x.IsPaused).HasColumnName("is_paused");
        b.Property(x => x.IsStopped).HasColumnName("is_stopped");
        b.Property(x => x.IsBlocked).HasColumnName("is_blocked");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<HouseholdMember>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.RecipientMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.NextDueOn, x.Id })
            .HasFilter("NOT is_paused AND NOT is_stopped AND NOT is_blocked AND next_due_on IS NOT NULL");
        b.HasIndex(x => new { x.HouseholdId, x.RecipientMemberId });
        b.HasIndex(x => new { x.HouseholdId, x.CreationOperationId }).IsUnique();
    }
}
