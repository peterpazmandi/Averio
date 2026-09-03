using Averio.Application.Common.Abstractions;
using Averio.Application.Identity;
using Averio.Infrastructure.Email;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using Averio.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Averio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AverioDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'AverioDatabase' is not configured.");
        }

        services.AddDbContextPool<AverioDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.Stores.MaxLengthForKeys = 128;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            })
            .AddEntityFrameworkStores<AverioDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        services
            .AddDataProtection()
            .SetApplicationName("Averio")
            .PersistKeysToDbContext<AverioDbContext>();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserSessionService, UserSessionService>();

        ConfigureEmail(services, configuration);

        return services;
    }

    private static void ConfigureEmail(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));

        var provider = configuration[$"{EmailOptions.SectionName}:Provider"];

        if (string.Equals(provider, "Brevo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ITransactionalEmailSender, BrevoTransactionalEmailSender>(
                client =>
                {
                    client.BaseAddress = new Uri("https://api.brevo.com/");
                });
        }
        else
        {
            services.AddScoped<ITransactionalEmailSender, DevelopmentTransactionalEmailSender>();
        }

        services.AddScoped<IEmailVerificationSender, EmailVerificationSender>();
    }
}