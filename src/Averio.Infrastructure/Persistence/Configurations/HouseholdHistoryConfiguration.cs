using Averio.Domain.Households;
using Averio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdHistoryConfiguration : IEntityTypeConfiguration<HouseholdHistory>
{
    public void Configure(EntityTypeBuilder<HouseholdHistory> b)
    {
        b.ToTable("household_history");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.HouseholdName).HasColumnName("household_name").HasMaxLength(120);
        b.Property(x => x.LeftAtUtc).HasColumnName("left_at_utc");
        b.Property(x => x.WasLastMember).HasColumnName("was_last_member");
        b.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
        b.HasIndex(x => new { x.UserId, x.LeftAtUtc });
        b.HasIndex(x => x.HouseholdId);
        b.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
