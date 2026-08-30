using System.Diagnostics;
using System.Globalization;
using Averio.Api.ErrorHandling;
using Averio.Infrastructure;
using Microsoft.AspNetCore.Localization;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id
            ?? context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("de-DE"),
        new CultureInfo("hu-HU")
    };

    options.DefaultRequestCulture =
        new RequestCulture("en-US");

    options.SupportedCultures =
        supportedCultures;

    options.SupportedUICultures =
        supportedCultures;

    options.ApplyCurrentCultureToResponseHeaders = true;

    // API language is explicitly controlled via Accept-Language.
    options.RequestCultureProviders =
    [
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddOpenApi(options =>
{
    // Keep the generated client on OpenAPI 3.0 for maximum
    // compatibility with client generators.
    options.OpenApiVersion =
        Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(section => section.Value)
    .Where(value => !string.IsNullOrWhiteSpace(value))
    .Cast<string>()
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "Frontend",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

builder.Services.AddInfrastructure(
    builder.Configuration);

var app = builder.Build();

app.UseRequestLocalization();

app.UseExceptionHandler();

app.UseStatusCodePages();

app.UseCors("Frontend");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Averio API")
            .DisableAgent();
    });
}

app.MapControllers();

app.Run();

public partial class Program;