using Averio.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class IconCatalogEntryConfiguration
    : IEntityTypeConfiguration<IconCatalogEntry>
{
    public void Configure(EntityTypeBuilder<IconCatalogEntry> builder)
    {
        builder.ToTable("icons");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Value).HasColumnName("value").HasMaxLength(32).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");

        builder.HasIndex(x => x.Value)
            .IsUnique()
            .HasDatabaseName("ux_icons_value");
        builder.HasIndex(x => x.SortOrder)
            .HasDatabaseName("ix_icons_sort_order");
    }
}
