using Averio.Domain.Recurring;
using Averio.Domain.Accounts;
using Averio.Domain.Categories;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class RecurringRuleConfiguration : IEntityTypeConfiguration<RecurringRule>
{
    public void Configure(EntityTypeBuilder<RecurringRule> b)
    {
        b.ToTable("recurring_rules", t => {
            t.HasCheckConstraint("ck_recurring_name", "length(btrim(name)) > 0");
            t.HasCheckConstraint("ck_recurring_amount", "amount > 0");
            t.HasCheckConstraint("ck_recurring_kind", "kind IN ('Income', 'Expense')");
            t.HasCheckConstraint("ck_recurring_visibility", "visibility IN ('Household', 'Private')");
            t.HasCheckConstraint("ck_recurring_frequency", "frequency IN ('Weekly', 'Monthly', 'Yearly')");
            t.HasCheckConstraint("ck_recurring_dates", "(ends_on IS NULL OR ends_on >= starts_on) AND occurrence_index >= 0 AND (next_due_on IS NULL OR next_due_on >= starts_on)");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.HouseholdId, x.Id });
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.AccountId).HasColumnName("account_id");
        b.Property(x => x.CategoryId).HasColumnName("category_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);
        b.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Visibility).HasColumnName("visibility").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Frequency).HasColumnName("frequency").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.StartsOn).HasColumnName("starts_on");
        b.Property(x => x.EndsOn).HasColumnName("ends_on");
        b.Property(x => x.OccurrenceIndex).HasColumnName("occurrence_index");
        b.Property(x => x.NextDueOn).HasColumnName("next_due_on");
        b.Property(x => x.Automatic).HasColumnName("automatic");
        b.Property(x => x.IsPaused).HasColumnName("is_paused");
        b.Property(x => x.IsStopped).HasColumnName("is_stopped");
        b.Property(x => x.IsBlocked).HasColumnName("is_blocked");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.AccountId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId });
        b.HasIndex(x => new { x.NextDueOn, x.Id }).HasFilter("automatic AND NOT is_paused AND NOT is_stopped AND NOT is_blocked");
    }
}
public sealed class RecurringOccurrenceConfiguration : IEntityTypeConfiguration<RecurringOccurrence>
{
    public void Configure(EntityTypeBuilder<RecurringOccurrence> b)
    {
        b.ToTable("recurring_occurrences", t => t.HasCheckConstraint("ck_recurring_receipt", "(transaction_id IS NULL AND amount IS NULL) OR (transaction_id IS NOT NULL AND amount IS NOT NULL AND amount > 0)"));
        b.HasKey(x => new { x.RuleId, x.DueOn });
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.RuleId).HasColumnName("rule_id");
        b.Property(x => x.DueOn).HasColumnName("due_on");
        b.Property(x => x.TransactionId).HasColumnName("transaction_id");
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        b.HasOne<RecurringRule>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.RuleId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TransactionId).IsUnique();
    }
}
