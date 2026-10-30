using Averio.Domain.Categories;
using Averio.Domain.Households;
using Averio.Domain.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class ReceiptCategoryRuleConfiguration : IEntityTypeConfiguration<ReceiptCategoryRule>
{
    public void Configure(EntityTypeBuilder<ReceiptCategoryRule> b)
    {
        b.ToTable("receipt_category_rules", t => t.HasCheckConstraint("ck_receipt_category_rule_key", "match_key ~ '^[0-9a-f]{64}$'"));
        b.HasKey(x => new { x.HouseholdId, x.MatchKey });
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.MatchKey).HasColumnName("match_key").HasMaxLength(64);
        b.Property(x => x.CategoryId).HasColumnName("category_id");
        b.Property(x => x.UpdatedByMemberId).HasColumnName("updated_by_member_id");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.UpdatedByMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Category>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.CategoryId);
        b.HasIndex(x => new { x.HouseholdId, x.UpdatedByMemberId });
    }
}
