using System.Text.Encodings.Web;

namespace Averio.Infrastructure.Email;

// Table layout and inline styles are intentional: email clients do not run Tailwind.
public static class InvitationEmailTemplate
{
    public static string Render(string baseUrl, string token, string title, string body, string button,
        string privacy, string expiry, string fallback, string footer)
    {
        static string E(string s) => HtmlEncoder.Default.Encode(s);
        var link = E(baseUrl.TrimEnd('/') + "/invitation#" + token);
        var logo = E(baseUrl.TrimEnd('/') + "/branding/averio-email-logo.png");
        return $$"""
            <!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{E(title)}}</title></head>
            <body style="margin:0;padding:0;background:#F3F8F6;color:#152033;font-family:Arial,Helvetica,sans-serif">
            <div style="display:none;font-size:1px;color:#F3F8F6;max-height:0;overflow:hidden">{{E(title)}} — {{E(expiry)}}</div>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F3F8F6"><tr><td align="center" style="padding:32px 16px">
            <table role="presentation" width="560" cellspacing="0" cellpadding="0" style="width:100%;max-width:560px">
            <tr><td style="padding:0 0 24px"><table role="presentation" cellspacing="0" cellpadding="0"><tr>
            <td style="background:#233044;border-radius:12px;padding:8px"><img src="{{logo}}" width="30" height="40" alt="" style="display:block;border:0"></td>
            <td style="padding-left:12px;color:#233044;font-size:26px;font-weight:bold">Averio</td></tr></table></td></tr>
            <tr><td style="background:#FFFFFF;border:1px solid #E4E7EC;border-left:4px solid #10B981;border-radius:18px;padding:32px 28px">
            <h1 style="margin:0 0 18px;font-size:26px;line-height:1.3;color:#152033">{{E(title)}}</h1>
            <p style="margin:0 0 24px;font-size:16px;line-height:1.7;color:#475467">{{E(body)}}</p>
            <table role="presentation" cellspacing="0" cellpadding="0"><tr><td bgcolor="#10B981" style="border-radius:11px"><a href="{{link}}" style="display:inline-block;border:14px solid #10B981;border-radius:11px;background:#10B981;color:#052E24;font-size:16px;font-weight:bold;text-decoration:none">{{E(button)}}</a></td></tr></table>
            <p style="margin:24px 0 0;padding:16px;background:#ECFDF5;border-radius:12px;font-size:14px;line-height:1.7;color:#065F46">{{E(privacy)}}</p>
            <p style="margin:20px 0 0;font-size:13px;line-height:1.7;color:#667085">{{E(expiry)}}</p>
            <p style="margin:16px 0 6px;font-size:12px;line-height:1.7;color:#667085">{{E(fallback)}}</p>
            <a href="{{link}}" style="font-size:12px;line-height:1.7;color:#047857;word-break:break-all;overflow-wrap:anywhere">{{link}}</a>
            </td></tr><tr><td style="padding:22px 8px;text-align:center;font-size:12px;line-height:1.7;color:#667085">{{E(footer)}}</td></tr>
            </table></td></tr></table></body></html>
            """;
    }
}
