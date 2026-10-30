using Amazon;
using Amazon.S3;
using Averio.Application.Receipts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
namespace Averio.Infrastructure.Receipts;

public static class ReceiptServices
{
    public static IServiceCollection AddReceiptServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ReceiptOptions>().Bind(config.GetSection("Receipts"))
            .Validate(x => x.DailyUploadLimit is >= 1 and <= 100, "Receipt upload limit must be between 1 and 100.").ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ReceiptOptions>>().Value);
        services.AddOptions<ReceiptStorageOptions>().Bind(config.GetSection("Receipts:Storage"))
            .Validate(x => !config.GetValue<bool>("Receipts:Enabled") || (!string.IsNullOrWhiteSpace(x.Bucket)
                && !string.IsNullOrWhiteSpace(x.Region)), "Enabled receipts require a private S3 bucket and region.").ValidateOnStart();
        services.AddOptions<GeminiReceiptOptions>().Bind(config.GetSection("Receipts:Gemini"))
            .Validate(x => !config.GetValue<bool>("Receipts:Enabled") || (!string.IsNullOrWhiteSpace(x.ApiKey)
                && System.Text.RegularExpressions.Regex.IsMatch(x.Model, "^[a-zA-Z0-9.-]{1,100}$")),
                "Enabled receipts require a Gemini API key and explicitly selected model.").ValidateOnStart();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IReceiptCategoryRuleStore, ReceiptCategoryRuleStore>();
        services.AddScoped<ReceiptCategorization>();
        services.AddSingleton<IReceiptImageNormalizer, ReceiptImageNormalizer>();
        if (string.IsNullOrWhiteSpace(config["Receipts:Storage:Bucket"]))
        {
            services.AddScoped<IReceiptDocumentStore, DisabledReceiptServices>();
            services.AddScoped<IReceiptExtractor, DisabledReceiptServices>();
            return services;
        }
        services.AddSingleton<IAmazonS3>(sp => new AmazonS3Client(new AmazonS3Config {
            RegionEndpoint = RegionEndpoint.GetBySystemName(sp.GetRequiredService<IOptions<ReceiptStorageOptions>>().Value.Region),
            Timeout = TimeSpan.FromSeconds(45), MaxErrorRetry = 0
        }));
        services.AddScoped<IReceiptDocumentStore, S3ReceiptDocumentStore>();
        services.AddHttpClient<IReceiptExtractor, GeminiReceiptExtractor>(client => client.Timeout = TimeSpan.FromSeconds(50))
            .RedactLoggedHeaders(["x-goog-api-key"]);
        return services;
    }
}
