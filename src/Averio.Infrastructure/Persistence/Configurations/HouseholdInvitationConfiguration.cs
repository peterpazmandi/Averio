using Averio.Domain.Households;
using Averio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdInvitationConfiguration : IEntityTypeConfiguration<HouseholdInvitation>
{
    public void Configure(EntityTypeBuilder<HouseholdInvitation> b)
    {
        b.ToTable("household_invitations", t => {
            t.HasCheckConstraint("ck_invitation_state", "\"State\" IN ('Pending','Accepted','Revoked')");
            t.HasCheckConstraint("ck_invitation_delivery", "\"DeliveryStatus\" IN ('Unknown','Sent','Failed')");
            t.HasCheckConstraint("ck_invitation_acceptance", "(\"State\" = 'Accepted') = (\"AcceptedAt\" IS NOT NULL AND \"AcceptedByUserId\" IS NOT NULL AND \"SourceHouseholdId\" IS NOT NULL)");
            t.HasCheckConstraint("ck_invitation_expiry", "\"ExpiresAt\" > \"CreatedAt\" AND \"SendCount\" > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Email).HasMaxLength(254).IsRequired();
        b.Property(x => x.NormalizedEmail).HasMaxLength(254).IsRequired();
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.State).HasMaxLength(16).IsRequired();
        b.Property(x => x.DeliveryStatus).HasMaxLength(16).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.HouseholdId, x.NormalizedEmail }).IsUnique().HasFilter("\"State\" = 'Pending'");
        b.HasOne<Household>().WithMany().HasForeignKey(x => x.HouseholdId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.InviterUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AcceptedByUserId).OnDelete(DeleteBehavior.Restrict);
        // SourceHouseholdId is an audit identifier, deliberately not an FK to the deleted empty household.
    }
}
