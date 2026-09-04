using Averio.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id).HasName("pk_users");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(320).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash");
        builder.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(x => x.PreferredCulture).HasColumnName("preferred_culture").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.LastLoginAtUtc).HasColumnName("last_login_at_utc");

        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_user_name");

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_email");
    }
}

internal sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims");
        builder.HasKey(x => x.Id).HasName("pk_user_claims");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ClaimType).HasColumnName("claim_type");
        builder.Property(x => x.ClaimValue).HasColumnName("claim_value");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_claims_user_id");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_claims_users_user_id");
    }
}

internal sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins");

        builder.HasKey(x => new { x.LoginProvider, x.ProviderKey })
            .HasName("pk_user_logins");

        builder.Property(x => x.LoginProvider)
            .HasColumnName("login_provider")
            .HasMaxLength(128);

        builder.Property(x => x.ProviderKey)
            .HasColumnName("provider_key")
            .HasMaxLength(128);

        builder.Property(x => x.ProviderDisplayName)
            .HasColumnName("provider_display_name");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_logins_user_id");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_logins_users_user_id");
    }
}

internal sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");

        builder.HasKey(x => new { x.UserId, x.LoginProvider, x.Name })
            .HasName("pk_user_tokens");

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(x => x.Value).HasColumnName("value");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_tokens_users_user_id");
    }
}