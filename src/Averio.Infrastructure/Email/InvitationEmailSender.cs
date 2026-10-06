using System.Text.Encodings.Web;
using Averio.Application.Households.Invitations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class InvitationEmailSender(ITransactionalEmailSender sender, IOptions<EmailOptions> options,
    IStringLocalizer<EmailMessages> text) : IInvitationEmailSender
{
    public async Task<bool> SendAsync(string email, string token, CancellationToken ct)
    {
        // The development sender logs whole email bodies. Never pass invitation credentials to it.
        if (!string.Equals(options.Value.Provider, "Brevo", StringComparison.OrdinalIgnoreCase)) return false;
        var link = HtmlEncoder.Default.Encode(options.Value.FrontendBaseUrl.TrimEnd('/') + "/invitation#" + token);
        var html = "<h1>" + HtmlEncoder.Default.Encode(text["Invitation_Subject"].Value) + "</h1><p>"
            + HtmlEncoder.Default.Encode(text["Invitation_Body"].Value) + "</p><a href=\"" + link + "\">"
            + HtmlEncoder.Default.Encode(text["Invitation_Button"].Value) + "</a>";
        try { await sender.SendAsync(email, text["Invitation_Subject"].Value, html, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; } // Persist failure without logging email content or credentials.
    }
}
