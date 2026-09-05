using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.VerifyEmail;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Averio.Application.Onboarding.CompleteOnboarding;
using Averio.Application.Onboarding.GetOnboardingStatus;
using Averio.Application.Households.UpdateHousehold;

namespace Averio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendEmailVerificationHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        services.AddScoped<GetOnboardingStatusHandler>();

        services.AddScoped<CompleteOnboardingHandler>();

        services.AddScoped<UpdateHouseholdHandler>();
        services.AddScoped<UpdateHouseholdValidator>();

        return services;
    }
}