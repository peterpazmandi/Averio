using Averio.Domain.Labels;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class TransactionLabelConfiguration
    : IEntityTypeConfiguration<TransactionLabel>
{
    public void Configure(EntityTypeBuilder<TransactionLabel> builder)
    {
        builder.ToTable("transaction_labels");

        builder.HasKey(x => new { x.TransactionId, x.LabelId })
            .HasName("pk_transaction_labels");

        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.LabelId).HasColumnName("label_id");

        builder.HasOne<Transaction>()
            .WithMany(x => x.Labels)
            .HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_transaction_labels_transactions");

        builder.HasOne<Label>().WithMany()
            .HasForeignKey(x => new { x.HouseholdId, x.LabelId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_labels_labels");

        builder.HasIndex(x => new { x.HouseholdId, x.LabelId })
            .HasDatabaseName("ix_transaction_labels_label");
    }
}
