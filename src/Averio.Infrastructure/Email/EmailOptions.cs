namespace Averio.Infrastructure.Email;

internal sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Development";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Averio";
    public string BrevoApiKey { get; set; } = string.Empty;
}