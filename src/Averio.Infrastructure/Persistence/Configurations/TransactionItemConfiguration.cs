using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class TransactionItemConfiguration
    : IEntityTypeConfiguration<TransactionItem>
{
    public void Configure(EntityTypeBuilder<TransactionItem> builder)
    {
        builder.ToTable("transaction_items", table =>
        {
            table.HasCheckConstraint("ck_transaction_items_amount", "amount > 0");
            table.HasCheckConstraint("ck_transaction_items_position", "position >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.Position).HasColumnName("position");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(160).IsRequired();
        builder.Property(x => x.CategoryName).HasColumnName("category_name").HasMaxLength(80);
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);

        builder.HasOne<Transaction>()
            .WithMany(x => x.Items)
            .HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_transaction_items_transactions");

        builder.HasIndex(x => new { x.HouseholdId, x.TransactionId, x.Position })
            .IsUnique()
            .HasDatabaseName("ux_transaction_items_transaction_position");
    }
}
