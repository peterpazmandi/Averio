
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Averio.Api.Authentication;
using Averio.Api.ErrorHandling;
using Averio.Api.Security;
using Averio.Application;
using Averio.Application.Common.Abstractions;
using Averio.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails(
    options =>
    {
        options.CustomizeProblemDetails =
            context =>
            {
                context.ProblemDetails
                    .Extensions["traceId"] =
                    Activity.Current?.Id
                    ?? context.HttpContext
                        .TraceIdentifier;
            };
    });

builder.Services
    .AddExceptionHandler<
        GlobalExceptionHandler>();

builder.Services.AddLocalization(
    options =>
    {
        options.ResourcesPath = "Resources";
    });

builder.Services.Configure<RequestLocalizationOptions>(options =>
    {
        var supportedCultures =
            new[]
            {
                new CultureInfo("en-US"),
                new CultureInfo("de-DE"),
                new CultureInfo("hu-HU")
            };

        options.DefaultRequestCulture = new RequestCulture("en-US");
        options.SupportedCultures = supportedCultures;
        options.SupportedUICultures = supportedCultures;
        options.ApplyCurrentCultureToResponseHeaders = true;
        options.RequestCultureProviders =
        [
            new AcceptLanguageHeaderRequestCultureProvider()
        ];
    });

builder.Services.AddApplication();

builder.Services.AddInfrastructure(builder.Configuration);

/*
 * Identity cookie authentication.
 */
builder.Services
    .AddAuthentication(
        options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
        })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(
    options =>
    {
        options.Cookie.Name =
            builder.Environment.IsDevelopment()
                ? "averio.auth"
                : "__Host-averio.auth";

        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.Path = "/";

        /*
         * averioapp.com and api.averioapp.com
         * are different origins but the same site.
         */
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin =
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };

        options.Events.OnRedirectToAccessDenied =
            context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("receipt-uploads", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

        /*
         * Anonymous, email-sending endpoints: throttled per client IP to
         * limit outbound email volume an attacker can trigger. A per-account
         * cooldown is enforced separately at the application layer.
         */
        options.AddPolicy(
            RateLimiterPolicies.PasswordResetRequests,
            httpContext => RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        SegmentsPerWindow = 3,
                        QueueLimit = 0
                    }));
    });

builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";

        options.Cookie.Name = builder.Environment.IsDevelopment()
                ? "averio.csrf"
                : "__Host-averio.csrf";

        options.Cookie.HttpOnly = true;
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
    });

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddOpenApi( options =>
    {
        options.OpenApiVersion =
            Microsoft.OpenApi
                .OpenApiSpecVersion
                .OpenApi3_0;

        options.AddSchemaTransformer((schema, context, _) =>
            {
                var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type)
                    ?? context.JsonTypeInfo.Type;

                if (type == typeof(byte)
                    || type == typeof(sbyte)
                    || type == typeof(short)
                    || type == typeof(ushort)
                    || type == typeof(int)
                    || type == typeof(uint)
                    || type == typeof(long)
                    || type == typeof(ulong))
                {
                    // ASP.NET's generated integer schemas currently include a
                    // format and pattern but can omit the JSON Schema type.
                    // Explicitly declaring it keeps generated clients strongly typed.
                    schema.Type = Microsoft.OpenApi.JsonSchemaType.Integer;
                }

                return Task.CompletedTask;
            });
    });

var allowedOrigins = builder.Configuration
        .GetSection(
            "Cors:AllowedOrigins")
        .GetChildren()
        .Select(x => x.Value)
        .Where(x =>
            !string.IsNullOrWhiteSpace(x))
        .Cast<string>()
        .ToArray();

builder.Services.AddCors(options =>
    {
        options.AddPolicy(
            "Frontend",
            policy =>
            {
                policy
                    .WithOrigins(
                        allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
    });

var app = builder.Build();

// Operational identity without changing the generated API contract.
var buildIdentity = typeof(Program).Assembly
    .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
    .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
    .Single().InformationalVersion;
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Averio-Version"] = buildIdentity;
    await next(context);
});

/*
 * Trust HTTPS scheme forwarding only from the configured
 * immediate reverse proxy.
 *
 * This must execute before authentication, antiforgery,
 * and other HTTPS-sensitive middleware.
 */
var isProductionLike =
    app.Environment.IsProduction()
    || app.Environment.IsEnvironment("Integration");

if (isProductionLike)
{
    var proxyIpValue = app.Configuration["ReverseProxy:KnownProxyIp"];

    if (string.IsNullOrWhiteSpace(proxyIpValue)
        || !IPAddress.TryParse(proxyIpValue, out var proxyIp))
    {
        throw new InvalidOperationException(
            $"{app.Environment.EnvironmentName} requires a valid " +
            "ReverseProxy:KnownProxyIp.");
    }

    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    };

    forwardedHeadersOptions.KnownProxies.Add(proxyIp);

    if (proxyIp.AddressFamily == AddressFamily.InterNetwork)
    {
        forwardedHeadersOptions.KnownProxies.Add(
            proxyIp.MapToIPv6());
    }

    app.UseForwardedHeaders(forwardedHeadersOptions);
}

app.UseRequestLocalization();

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseCors("Frontend");

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();

app.UseMiddleware<
    CsrfProtectionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(
        options =>
        {
            options
                .WithTitle("Averio API")
                .DisableAgent();
        });
}

app.MapControllers();

app.Run();

public partial class Program;


