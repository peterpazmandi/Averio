using Averio.Domain.Households;
using Averio.Domain.Notifications;
using Averio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class UserNotificationConfiguration : IEntityTypeConfiguration<UserNotification>
{
    public void Configure(EntityTypeBuilder<UserNotification> b)
    {
        b.ToTable("user_notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.Type).HasColumnName("type").HasMaxLength(48);
        b.Property(x => x.TargetId).HasColumnName("target_id");
        b.Property(x => x.OccurrenceKey).HasColumnName("occurrence_key").HasMaxLength(80);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.ReadAt).HasColumnName("read_at");
        b.Property(x => x.DismissedAt).HasColumnName("dismissed_at");
        b.Property(x => x.ResolvedAt).HasColumnName("resolved_at");
        b.HasIndex(x => new { x.RecipientUserId, x.Type, x.TargetId, x.OccurrenceKey }).IsUnique()
            .HasDatabaseName("ux_user_notifications_recipient_event");
        b.HasIndex(x => new { x.RecipientUserId, x.CreatedAt, x.Id });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
