using System.Globalization;
using System.Resources;
using Averio.Infrastructure;
using Averio.Infrastructure.Email;

internal static class InvitationEmailPreview
{
    public static void Write(string directory)
    {
        Directory.CreateDirectory(directory);
        var resources = new ResourceManager("Averio.Infrastructure.Resources.EmailMessages", typeof(EmailMessages).Assembly);
        foreach (var cultureName in new[] { "en-US", "de-DE", "hu-HU" })
        {
            var culture = CultureInfo.GetCultureInfo(cultureName);
            string Text(string key) => resources.GetString(key, culture)
                ?? throw new InvalidOperationException("Missing email resource: " + key);
            var title = string.Format(culture, Text("Invitation_Title"), "Pazmándi család & a longer household name");
            var html = InvitationEmailTemplate.Render("https://preview.averio.example.test", "PREVIEW_ONLY_" + new string('A', 43),
                title, Text("Invitation_Body"), Text("Invitation_Button"), Text("Invitation_Privacy"),
                Text("Invitation_Expiry"), Text("Invitation_Fallback"), Text("Invitation_Footer"));
            File.WriteAllText(Path.Combine(directory, cultureName + ".html"), html);
        }
        Console.WriteLine("PASS invitation previews rendered from actual EN/DE/HU resources with synthetic credentials");
    }
}
