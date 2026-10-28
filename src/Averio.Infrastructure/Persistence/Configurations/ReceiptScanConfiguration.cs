using Averio.Domain.Receipts;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class ReceiptScanConfiguration : IEntityTypeConfiguration<ReceiptScan>
{
    public void Configure(EntityTypeBuilder<ReceiptScan> b)
    {
        b.ToTable("receipt_scans", t => {
            t.HasCheckConstraint("ck_receipt_status", "status IN ('Uploading','Queued','Processing','Ready','Failed','Discarded')");
            t.HasCheckConstraint("ck_receipt_attempts", "attempts BETWEEN 0 AND 3");
            t.HasCheckConstraint("ck_receipt_bytes", "byte_length > 0 AND byte_length <= 10485760");
            t.HasCheckConstraint("ck_receipt_lease", "(status = 'Processing' AND lease_token IS NOT NULL AND lease_until IS NOT NULL) OR (status <> 'Processing' AND lease_token IS NULL AND lease_until IS NULL)");
            t.HasCheckConstraint("ck_receipt_draft", "(status = 'Ready') = (draft_json IS NOT NULL)");
            t.HasCheckConstraint("ck_receipt_expiry", "expires_at > created_at");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.ObjectKey).HasColumnName("object_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.ByteLength).HasColumnName("byte_length");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.LeaseToken).HasColumnName("lease_token");
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.DraftJson).HasColumnName("draft_json").HasMaxLength(262144);
        b.Property(x => x.FailureCode).HasColumnName("failure_code").HasMaxLength(50);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.DocumentDeletedAt).HasColumnName("document_deleted_at");
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId, x.OperationId }).IsUnique();
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => x.ExpiresAt);
    }
}
