using Averio.Domain.Transactions;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Averio.Infrastructure.Persistence.Configurations;

public sealed class SavedTransactionViewConfiguration : IEntityTypeConfiguration<SavedTransactionView>
{
    public void Configure(EntityTypeBuilder<SavedTransactionView> b)
    {
        b.ToTable("saved_transaction_views", t => t.HasCheckConstraint("ck_saved_views_name", "length(btrim(name)) > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.HouseholdId).HasColumnName("household_id");
        b.Property(x => x.HouseholdMemberId).HasColumnName("household_member_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(80).IsRequired();
        b.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(160).IsRequired();
        b.Property(x => x.FiltersJson).HasColumnName("filters_json").HasMaxLength(4000).IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasOne<HouseholdMember>().WithMany().HasForeignKey(x => new { x.HouseholdId, x.HouseholdMemberId })
            .HasPrincipalKey(x => new { x.HouseholdId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.HouseholdId, x.HouseholdMemberId, x.NormalizedName }).IsUnique();
    }
}
