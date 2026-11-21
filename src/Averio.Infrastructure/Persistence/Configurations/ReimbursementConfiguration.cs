using Averio.Domain.Recurring;
using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class ReimbursementClaimConfiguration : IEntityTypeConfiguration<ReimbursementClaim>
{
    public void Configure(EntityTypeBuilder<ReimbursementClaim> b)
    {
        b.ToTable("reimbursement_claims", t => {
            t.HasCheckConstraint("ck_claim_amount", "potential_amount > 0 AND original_payment_amount >= potential_amount");
            t.HasCheckConstraint("ck_claim_provider", "length(btrim(provider)) > 0");
            t.HasCheckConstraint("ck_claim_currency", "currency IN ('EUR','USD','HUF')");
            t.HasCheckConstraint("ck_claim_status", "status IN ('NeedsSubmission','Submitted','Rejected','Cancelled','PartiallyReceived','Received')");
            t.HasCheckConstraint("ck_claim_submission", "(status NOT IN ('Submitted','Rejected','PartiallyReceived','Received') OR (submitted_on IS NOT NULL AND expected_on IS NOT NULL)) AND (submitted_on IS NULL OR submitted_on >= payment_on) AND (expected_on IS NULL OR expected_on >= submitted_on) AND payment_delay_days BETWEEN 0 AND 365");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.HouseholdId, x.Id });
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.CreationFingerprint).HasColumnName("creation_fingerprint").HasMaxLength(64);
        b.Property(x => x.RuleId).HasColumnName("rule_id");
        b.Property(x => x.TransactionId).HasColumnName("transaction_id");
        b.Property(x => x.DestinationAccountId).HasColumnName("destination_account_id");
        b.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(160);
        b.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
        b.Property(x => x.PotentialAmount).HasColumnName("potential_amount").HasPrecision(19,4);
        b.Property(x => x.OriginalPaymentAmount).HasColumnName("original_payment_amount").HasPrecision(19,4);
        b.Property(x => x.PaymentOn).HasColumnName("payment_on");
        b.Property(x => x.PaymentDelayDays).HasColumnName("payment_delay_days");
        b.Property(x => x.SubmissionReminderOn).HasColumnName("submission_reminder_on");
        b.Property(x => x.SubmittedOn).HasColumnName("submitted_on");
        b.Property(x => x.ExpectedOn).HasColumnName("expected_on");
        b.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(160);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Ignore(x => x.ExpectedAmount);
        b.Ignore(x => x.ReceivedAmount);
        b.Ignore(x => x.OutstandingAmount);
        b.Navigation(x => x.Payments).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.Navigation(x => x.Events).UsePropertyAccessMode(PropertyAccessMode.Field);
        b.HasOne<RecurringRule>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.RuleId }).HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Transaction>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.TransactionId }).HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Account>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.DestinationAccountId }).HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId }).HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.TransactionId).IsUnique();
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId, x.Status });
    }
}
public sealed class ReimbursementClaimEventConfiguration : IEntityTypeConfiguration<ReimbursementClaimEvent>
{
    public void Configure(EntityTypeBuilder<ReimbursementClaimEvent> b)
    {
        b.ToTable("reimbursement_claim_events");
        b.Property(x => x.Sequence).HasColumnName("sequence");
        b.Property(x => x.ChangeKind).HasColumnName("change_kind").HasMaxLength(24);
        b.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(160);
        b.Property(x => x.PotentialAmount).HasColumnName("potential_amount").HasPrecision(19, 4);
        b.Property(x => x.DestinationAccountId).HasColumnName("destination_account_id");
        b.Property(x => x.PaymentDelayDays).HasColumnName("payment_delay_days");
        b.Property(x => x.SubmissionReminderOn).HasColumnName("submission_reminder_on");
        b.Property(x => x.ExpectedOn).HasColumnName("expected_on");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.ClaimId).HasColumnName("claim_id");
        b.Property(x => x.ActorMemberId).HasColumnName("actor_member_id");
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(24);
        b.Property(x => x.SubmittedOn).HasColumnName("submitted_on");
        b.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(160);
        b.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        b.HasOne<ReimbursementClaim>().WithMany(x => x.Events).HasForeignKey(x => new { x.HouseholdId, x.ClaimId }).HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.ActorMemberId }).HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
