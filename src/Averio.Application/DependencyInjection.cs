using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.VerifyEmail;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Averio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(
            typeof(DependencyInjection).Assembly);

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendEmailVerificationHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        return services;
    }
}