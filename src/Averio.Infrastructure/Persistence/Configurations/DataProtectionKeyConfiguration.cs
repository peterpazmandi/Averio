using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.HasKey(x => x.Id).HasName("pk_data_protection_keys");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FriendlyName).HasColumnName("friendly_name");
        builder.Property(x => x.Xml).HasColumnName("xml");
    }
}