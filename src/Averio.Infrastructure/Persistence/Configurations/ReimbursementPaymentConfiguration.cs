using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Averio.Infrastructure.Persistence.Configurations;
public sealed class ReimbursementPaymentConfiguration : IEntityTypeConfiguration<ReimbursementPayment>
{
    public void Configure(EntityTypeBuilder<ReimbursementPayment> b)
    {
        b.ToTable("reimbursement_payments", t => {
            t.HasCheckConstraint("ck_reimbursement_payment_amount", "amount > 0");
            t.HasCheckConstraint("ck_reimbursement_payment_unlink", "(unlinked_at IS NULL) = (unlinked_by_member_id IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.ClaimId).HasColumnName("claim_id");
        b.Property(x => x.CreditTransactionId).HasColumnName("credit_transaction_id");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(64);
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19,4);
        b.Property(x => x.ReceivedOn).HasColumnName("received_on");
        b.Property(x => x.LinkedByMemberId).HasColumnName("linked_by_member_id");
        b.Property(x => x.LinkedAt).HasColumnName("linked_at");
        b.Property(x => x.UnlinkedByMemberId).HasColumnName("unlinked_by_member_id");
        b.Property(x => x.UnlinkedAt).HasColumnName("unlinked_at");
        b.HasIndex(x => new { x.HouseholdId, x.OperationId }).IsUnique();
        b.HasIndex(x => x.CreditTransactionId).IsUnique().HasFilter("unlinked_at IS NULL");
        b.HasOne<ReimbursementClaim>().WithMany(c => c.Payments).HasForeignKey(x => new { x.HouseholdId, x.ClaimId }).HasPrincipalKey(c => new { c.HouseholdId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.CreditTransactionId }).HasPrincipalKey(c => new { c.HouseholdId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.LinkedByMemberId }).HasPrincipalKey(c => new { c.HouseholdId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.UnlinkedByMemberId }).HasPrincipalKey(c => new { c.HouseholdId, c.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
