using Averio.Domain.Transactions;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Averio.Infrastructure.Persistence.Configurations;
internal sealed class ItemRefundConfiguration : IEntityTypeConfiguration<ItemRefund>
{
    public void Configure(EntityTypeBuilder<ItemRefund> b)
    {
        b.ToTable("item_refunds", t => {
            t.HasCheckConstraint("ck_item_refunds_amount", "amount > 0");
            t.HasCheckConstraint("ck_item_refunds_distinct", "transaction_id <> credit_transaction_id");
            t.HasCheckConstraint("ck_item_refunds_cancellation", "(cancelled_at IS NULL) = (cancelled_by_member_id IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.TransactionId).HasColumnName("transaction_id");
        b.Property(x => x.TransactionItemId).HasColumnName("transaction_item_id");
        b.Property(x => x.CreditTransactionId).HasColumnName("credit_transaction_id");
        b.Property(x => x.OperationId).HasColumnName("operation_id");
        b.Property(x => x.CreatedByMemberId).HasColumnName("created_by_member_id");
        b.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19,4);
        b.Property(x => x.ReceivedOn).HasColumnName("received_on");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
        b.Property(x => x.CancelledByMemberId).HasColumnName("cancelled_by_member_id");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasMaxLength(64).IsRequired();
        b.HasOne<TransactionItem>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.TransactionId, x.TransactionItemId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.TransactionId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.CreditTransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.CreatedByMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.CancelledByMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.HouseholdId, x.OperationId }).IsUnique();
        b.HasIndex(x => x.CreditTransactionId).IsUnique();
        b.HasIndex(x => new { x.HouseholdId, x.TransactionId });
    }
}
