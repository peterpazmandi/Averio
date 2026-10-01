using Averio.Domain.Categories;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class CategoryPinConfiguration : IEntityTypeConfiguration<CategoryPin>
{
    public void Configure(EntityTypeBuilder<CategoryPin> builder)
    {
        builder.ToTable("category_pins");
        builder.HasKey(x => new { x.HouseholdMemberId, x.CategoryId });
        builder.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        builder.Property(x => x.CategoryId).HasColumnName("category_id");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => x.HouseholdMemberId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.CategoryId);
    }
}
