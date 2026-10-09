using System.Text.Encodings.Web;

namespace Averio.Infrastructure.Email;

// Email clients require presentation tables and inline fallbacks rather than Tailwind.
public static class InvitationEmailTemplate
{
    public static string Render(string baseUrl, string token, string title, string body, string button,
        string privacy, string expiry, string fallback, string footer)
    {
        static string E(string s) => HtmlEncoder.Default.Encode(s);
        var link = E(baseUrl.TrimEnd('/') + "/invitation#" + token);
        var logo = E(baseUrl.TrimEnd('/') + "/branding/averio-email-logo.png");
        // Outlook's Word renderer needs a VML button and an explicit desktop width.
        // Both button variants and the fallback link use the same escaped credential.
        return $$"""
            <!doctype html>
            <html xmlns:v="urn:schemas-microsoft-com:vml" xmlns:w="urn:schemas-microsoft-com:office:word" xmlns:o="urn:schemas-microsoft-com:office:office">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <meta name="color-scheme" content="light dark">
            <meta name="supported-color-schemes" content="light dark">
            <title>{{E(title)}}</title>
            <style>
            :root { color-scheme:light dark; supported-color-schemes:light dark; }
            table { border-spacing:0; mso-table-lspace:0pt; mso-table-rspace:0pt; }
            a { text-decoration:none; }
            @media (prefers-color-scheme:dark) {
              .av-email-page { background-color:#07101F !important; }
              .av-email-card { background-color:#0D1726 !important; border-color:#233044 !important; border-left-color:#10B981 !important; }
              .av-email-heading { color:#F3F6FA !important; }
              .av-email-body { color:#CBD5E1 !important; }
              .av-email-muted { color:#AAB4C4 !important; }
              .av-email-privacy { background-color:#132D2E !important; color:#A7F3D0 !important; }
              .av-email-link { color:#6EE7B7 !important; }
              .av-email-button { background-color:#10B981 !important; color:#052E24 !important; }
            }
            [data-ogsc] .av-email-page, [data-ogsb] .av-email-page { background-color:#07101F !important; }
            [data-ogsc] .av-email-card, [data-ogsb] .av-email-card { background-color:#0D1726 !important; border-color:#233044 !important; border-left-color:#10B981 !important; }
            [data-ogsc] .av-email-heading { color:#F3F6FA !important; }
            [data-ogsc] .av-email-body { color:#CBD5E1 !important; }
            [data-ogsc] .av-email-muted { color:#AAB4C4 !important; }
            [data-ogsc] .av-email-privacy, [data-ogsb] .av-email-privacy { background-color:#132D2E !important; color:#A7F3D0 !important; }
            [data-ogsc] .av-email-link { color:#6EE7B7 !important; }
            [data-ogsc] .av-email-button, [data-ogsb] .av-email-button { background-color:#10B981 !important; color:#052E24 !important; }
            </style>
            <!--[if mso]>
            <xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml>
            <![endif]-->
            </head>
            <body class="av-email-page" bgcolor="#F3F8F6" style="margin:0;padding:0;background-color:#F3F8F6;color:#152033;font-family:Arial,Helvetica,sans-serif">
            <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all">{{E(title)}} — {{E(expiry)}}</div>
            <table class="av-email-page" role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" bgcolor="#F3F8F6" style="width:100%;background-color:#F3F8F6">
            <tr><td align="center" style="padding:32px 16px">
            <!--[if mso]><table role="presentation" width="560" border="0" cellspacing="0" cellpadding="0"><tr><td><![endif]-->
            <table role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" style="width:100%;max-width:560px">
            <tr><td style="padding:0 0 24px">
            <table role="presentation" border="0" cellspacing="0" cellpadding="0"><tr>
            <td bgcolor="#233044" style="background-color:#233044;border-radius:12px;padding:8px"><img src="{{logo}}" width="30" height="40" alt="" style="display:block;border:0"></td>
            <td class="av-email-heading" style="padding-left:12px;color:#233044;font-size:26px;line-height:34px;font-weight:bold">Averio</td>
            </tr></table>
            </td></tr>
            <tr><td class="av-email-card" bgcolor="#FFFFFF" style="background-color:#FFFFFF;border:1px solid #E4E7EC;border-left:4px solid #10B981;border-radius:18px;padding:32px 24px">
            <h1 class="av-email-heading" style="margin:0 0 18px;font-size:26px;line-height:34px;mso-line-height-rule:at-least;color:#152033">{{E(title)}}</h1>
            <p class="av-email-body" style="margin:0 0 24px;font-size:16px;line-height:27px;color:#475467">{{E(body)}}</p>
            <!--[if mso]>
            <v:roundrect href="{{link}}" style="height:48px;v-text-anchor:middle;width:260px;" arcsize="23%" stroke="f" fillcolor="#10B981">
            <w:anchorlock/>
            <center style="color:#052E24;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:bold">{{E(button)}}</center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-->
            <table role="presentation" border="0" cellspacing="0" cellpadding="0" style="mso-hide:all"><tr>
            <td class="av-email-button" align="center" bgcolor="#10B981" style="background-color:#10B981;border-radius:11px">
            <a class="av-email-button" href="{{link}}" style="display:inline-block;padding:14px 24px;background-color:#10B981;border-radius:11px;color:#052E24;font-size:16px;line-height:20px;font-weight:bold;text-decoration:none">{{E(button)}}</a>
            </td></tr></table>
            <!--<![endif]-->
            <table role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" style="width:100%"><tr><td style="padding-top:24px">
            <table role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" style="width:100%"><tr>
            <td class="av-email-privacy" bgcolor="#ECFDF5" style="padding:16px;background-color:#ECFDF5;border-radius:12px;font-size:14px;line-height:24px;color:#065F46">{{E(privacy)}}</td>
            </tr></table>
            </td></tr></table>
            <p class="av-email-muted" style="margin:20px 0 0;font-size:13px;line-height:22px;color:#667085">{{E(expiry)}}</p>
            <p class="av-email-muted" style="margin:16px 0 6px;font-size:12px;line-height:20px;color:#667085">{{E(fallback)}}</p>
            <a class="av-email-link" href="{{link}}" style="font-size:12px;line-height:20px;color:#047857;text-decoration:underline;word-break:break-all;overflow-wrap:anywhere">{{link}}</a>
            </td></tr>
            <tr><td class="av-email-muted" style="padding:22px 8px;text-align:center;font-size:12px;line-height:20px;color:#667085">{{E(footer)}}</td></tr>
            </table>
            <!--[if mso]></td></tr></table><![endif]-->
            </td></tr></table>
            </body></html>
            """;
    }
}
