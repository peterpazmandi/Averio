using Averio.Domain.Households;
using Averio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable(
            "households",
            table =>
        {
            table.HasCheckConstraint(
                "ck_households_country_code",
                """
                country_code IS NULL
                OR country_code ~ '^[A-Z]{2}$'
                """);

            table.HasCheckConstraint(
                "ck_households_preferred_currency",
                """
                preferred_currency IS NULL
                OR preferred_currency IN ('HUF', 'EUR', 'USD')
                """);
        });
        builder.HasKey(x => x.Id).HasName("pk_households");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.Property(x => x.CountryCode).HasColumnName("country_code").HasColumnType("char(2)");

        builder.Property(x => x.PreferredCurrency)
            .HasColumnName("preferred_currency")
            .HasConversion<string>()
            .HasMaxLength(3);

        builder.Property(x => x.OnboardingCompletedAtUtc)
            .HasColumnName("onboarding_completed_at_utc");

        builder.Navigation(x => x.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class HouseholdMemberConfiguration : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable(
            "household_members",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_household_members_role",
                    "role IN ('owner', 'member')");
            });

        builder.HasKey(x => x.Id).HasName("pk_household_members");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(16)
            .HasConversion(
                role => role.ToString().ToLowerInvariant(),
                value => Enum.Parse<HouseholdRole>(value, ignoreCase: true));

        builder.Property(x => x.JoinedAtUtc)
            .HasColumnName("joined_at_utc")
            .IsRequired();

        builder.HasOne<Household>()
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_household_members_households_household_id");

        builder.HasOne<ApplicationUser>()
            .WithOne(x => x.HouseholdMembership)
            .HasForeignKey<HouseholdMember>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_household_members_users_user_id");

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("ux_household_members_user_id");

        // Named at creation so EF keeps this distinct from the filtered owner index below
        // (two HasIndex calls on the same property both defaulting to no name collapse into one index).
        builder.HasIndex(x => x.HouseholdId, "ix_household_members_household_id");

        builder.HasIndex(x => x.HouseholdId, "ux_household_members_household_owner")
            .IsUnique()
            .HasFilter("role = 'owner'");
    }
}