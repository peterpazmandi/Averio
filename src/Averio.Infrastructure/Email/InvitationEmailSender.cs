using System.Text.Encodings.Web;
using Averio.Application.Households.Invitations;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class InvitationEmailSender(ITransactionalEmailSender sender, IOptions<EmailOptions> options,
    IStringLocalizer<EmailMessages> text) : IInvitationEmailSender
{
    public async Task<bool> SendAsync(string email, string token, string householdName, CancellationToken ct)
    {
        // The development sender logs whole email bodies. Never pass invitation credentials to it.
        if (!string.Equals(options.Value.Provider, "Brevo", StringComparison.OrdinalIgnoreCase)) return false;
        var html = InvitationEmailTemplate.Render(options.Value.FrontendBaseUrl, token,
            text["Invitation_Title", householdName].Value, text["Invitation_Body"].Value,
            text["Invitation_Button"].Value, text["Invitation_Privacy"].Value,
            text["Invitation_Expiry"].Value, text["Invitation_Fallback"].Value, text["Invitation_Footer"].Value);
        try { await sender.SendAsync(email, text["Invitation_Subject"].Value, html, ct); return true; }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return false; } // Persist failure without logging email content or credentials.
    }
}

