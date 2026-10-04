using Averio.Domain.Activities;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> b)
    {
        b.ToTable("activities", t =>
        {
            t.HasCheckConstraint("ck_activities_dates", "ends_on IS NULL OR starts_on IS NULL OR ends_on >= starts_on");
            t.HasCheckConstraint("ck_activities_name", "length(btrim(name)) > 0");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.HouseholdId, x.HouseholdMemberId, x.Id });
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        b.Property(x => x.StartsOn).HasColumnName("starts_on");
        b.Property(x => x.EndsOn).HasColumnName("ends_on");
        b.Property(x => x.IsClosed).HasColumnName("is_closed");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId, x.IsClosed, x.Name });
    }
}
public sealed class ActivityAssignmentConfiguration : IEntityTypeConfiguration<ActivityAssignment>
{
    public void Configure(EntityTypeBuilder<ActivityAssignment> b)
    {
        b.ToTable("activity_assignments");
        b.HasKey(x => new { x.HouseholdMemberId, x.TransactionId });
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.TransactionId).HasColumnName("transaction_id");
        b.Property(x => x.ActivityId).HasColumnName("activity_id");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<Activity>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId, x.ActivityId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.HouseholdMemberId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
