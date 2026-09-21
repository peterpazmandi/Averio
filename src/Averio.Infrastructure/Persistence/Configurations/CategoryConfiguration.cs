using Averio.Domain.Categories;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table =>
        {
            table.HasCheckConstraint("ck_categories_kind", "kind IN ('Expense', 'Income', 'Both')");
            table.HasCheckConstraint(
                "ck_categories_scope",
                "(is_base AND household_id IS NULL) OR (NOT is_base AND household_id IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_categories_name_source",
                "(is_base AND system_key IS NOT NULL AND name IS NULL) OR "
                + "(NOT is_base AND system_key IS NULL AND name IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_categories_accent_color",
                "accent_color ~ '^#[0-9A-Fa-f]{6}$'");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.ParentId).HasColumnName("parent_id");
        builder.Property(x => x.SystemKey).HasColumnName("system_key").HasMaxLength(80);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().HasMaxLength(12);
        builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(32).IsRequired();
        builder.Property(x => x.AccentColor).HasColumnName("accent_color").HasMaxLength(7).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.Property(x => x.IsBase).HasColumnName("is_base");
        builder.Property(x => x.IsArchived).HasColumnName("is_archived");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne<Household>().WithMany()
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_categories_households");

        builder.HasOne<Category>().WithMany()
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_categories_parent");

        builder.HasIndex(x => x.SystemKey)
            .IsUnique()
            .HasFilter("is_base")
            .HasDatabaseName("ux_categories_system_key");

        builder.HasIndex(x => new { x.HouseholdId, x.ParentId, x.SortOrder })
            .HasDatabaseName("ix_categories_household_parent_order");
        builder.HasIndex(x => new { x.IsBase, x.Kind, x.IsArchived })
            .HasDatabaseName("ix_categories_base_kind_status");
    }
}
