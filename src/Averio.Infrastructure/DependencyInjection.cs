using Averio.Application.Common.Abstractions;
using Averio.Application.Identity;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.RequestPasswordReset;
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
            options.UseNpgsql(connectionString);
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

                const string passwordResetProvider = "AverioPasswordReset";

                options.Tokens.ProviderMap.Add(
                    passwordResetProvider,
                    new TokenProviderDescriptor(typeof(PasswordResetTokenProvider<ApplicationUser>)));

                options.Tokens.PasswordResetTokenProvider =
                    passwordResetProvider;
            })
            .AddEntityFrameworkStores<AverioDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        services.AddOptions<PasswordResetTokenProviderOptions>();

        services.AddTransient<PasswordResetTokenProvider<ApplicationUser>>();

        services
            .AddDataProtection()
            .SetApplicationName("Averio")
            .PersistKeysToDbContext<AverioDbContext>();

        services.AddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<Averio.Application.PersonalMoney.IPersonalMoneyRepository, PersonalMoneyRepository>();
        services.AddScoped<Averio.Application.PersonalMoney.IRecurringAllowanceRepository, RecurringAllowanceRepository>();
        services.AddScoped<Averio.Application.Recurring.IRecurringRepository, RecurringRepository>();
        services.AddScoped<Averio.Application.Households.Invitations.IInvitationRepository, InvitationRepository>();
        services.AddScoped<Averio.Application.Households.Invitations.IInvitationEmailSender, InvitationEmailSender>();
        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<Averio.Application.Households.Invitations.IHouseholdHistoryRepository, HouseholdHistoryRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IAccountBalanceReader, AccountBalanceReader>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<Averio.Application.Categories.ManageCategories.ICategoryPinRepository, CategoryPinRepository>();
        services.AddScoped<Averio.Application.Categories.ManageCategories.ICategoryManagementRepository, CategoryManagementRepository>();
        services.AddScoped<IIconCatalogRepository, IconCatalogRepository>();
        services.AddScoped<Averio.Application.Transactions.SavedViews.ISavedViewRepository, SavedViewRepository>();
        services.AddScoped<ILabelRepository, LabelRepository>();
        services.AddScoped<Averio.Application.Transfers.ITransferRepository, TransferRepository>();
        services.AddScoped<Averio.Application.Activities.IActivityRepository, ActivityRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ITransactionAggregateRepository, TransactionAggregateRepository>();
        services.AddScoped<Averio.Application.Statistics.GetBoundaryCashFlow.IBoundaryFlowRepository, BoundaryFlowRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddSingleton<IPasswordResetRequestThrottle, PasswordResetRequestThrottle>();
        services.AddSingleton<IResendEmailVerificationThrottle, ResendEmailVerificationThrottle>();

        Receipts.ReceiptServices.AddReceiptServices(services, configuration);
        ConfigureEmail(services, configuration);

        return services;
    }

    private static void ConfigureEmail(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .Validate(options => InvitationEmailTemplate.IsSupportedLogoUrl(options.LogoUrl),
                "Email:LogoUrl must use HTTPS and a public DNS host, without user info or a fragment.")
            .ValidateOnStart();

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
        services.AddScoped<IPasswordResetSender, PasswordResetEmailSender>();

        services.AddSingleton<EmailVerificationQueue>();
        services.AddSingleton<IEmailVerificationQueue>(sp => sp.GetRequiredService<EmailVerificationQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<EmailVerificationQueue>());
        services.AddSingleton<PasswordResetEmailQueue>();
        services.AddSingleton<IPasswordResetEmailQueue>(sp => sp.GetRequiredService<PasswordResetEmailQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<PasswordResetEmailQueue>());
    }
}


