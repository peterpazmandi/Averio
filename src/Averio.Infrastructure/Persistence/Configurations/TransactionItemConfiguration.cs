using Averio.Domain.Categories;
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
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.TransactionId).HasColumnName("transaction_id");
        builder.Property(x => x.Position).HasColumnName("position");
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(160).IsRequired();
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.CategoryName).HasColumnName("category_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CategoryIcon).HasColumnName("category_icon").HasMaxLength(32).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(19, 4);

        builder.HasOne<Transaction>()
            .WithMany(x => x.Items)
            .HasForeignKey(x => new { x.HouseholdId, x.TransactionId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_transaction_items_transactions");

        builder.HasOne<Category>().WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_items_categories");

        builder.HasIndex(x => new { x.HouseholdId, x.TransactionId, x.Position })
            .IsUnique()
            .HasDatabaseName("ux_transaction_items_transaction_position");
        builder.HasIndex(x => x.CategoryId)
            .HasDatabaseName("ix_transaction_items_category");
    }
}
