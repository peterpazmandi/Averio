using System.Text.Encodings.Web;

namespace Averio.Infrastructure.Email;

// Email clients require presentation tables and inline fallbacks rather than Tailwind.
public static class InvitationEmailTemplate
{
    public static string Render(string baseUrl, string token, string title, string body, string button,
        string privacy, string expiry, string fallback, string footer)
    {
        static string E(string s) => HtmlEncoder.Default.Encode(s);
        var origin = baseUrl.TrimEnd('/');
        var link = E(origin + "/invitation#" + token);
        var logo = E(origin + "/branding/averio-email-logo.png");
        // Optional break opportunities affect only displayed text, never the href
        // or copied credential. Do not insert zero-width characters into the URL.
        var visibleLink = E(origin).Replace("/", "/<wbr>").Replace(".", ".<wbr>").Replace("-", "-<wbr>")
            + "<wbr>/invitation<wbr>#"
            + string.Join("<wbr>", token.Chunk(12).Select(part => E(new string(part))));
        // Keep each block in its own table row. Outlook may discard paragraph
        // margins or anchor padding, and dark-mode color rewrites vary by client.
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
            @media (prefers-color-scheme:dark) {
              .av-email-page, .av-email-footer { background-color:#07101F !important; color:#AAB4C4 !important; }
              .av-email-card { background-color:#0D1726 !important; color:#F3F6FA !important; border-color:#233044 !important; border-left-color:#10B981 !important; }
              .av-email-heading { background-color:#0D1726 !important; color:#F3F6FA !important; }
              .av-email-brand { background-color:#07101F !important; color:#F3F6FA !important; }
              .av-email-body { background-color:#0D1726 !important; color:#CBD5E1 !important; }
              .av-email-muted { background-color:#0D1726 !important; color:#AAB4C4 !important; }
              .av-email-privacy { background-color:#132D2E !important; color:#A7F3D0 !important; }
              .av-email-link { background-color:#0D1726 !important; color:#6EE7B7 !important; }
              .av-email-button { background-color:#10B981 !important; color:#052E24 !important; }
            }
            [data-ogsc] .av-email-page, [data-ogsb] .av-email-page,
            [data-ogsc] .av-email-footer, [data-ogsb] .av-email-footer { background-color:#07101F !important; color:#AAB4C4 !important; }
            [data-ogsc] .av-email-card, [data-ogsb] .av-email-card { background-color:#0D1726 !important; color:#F3F6FA !important; border-color:#233044 !important; border-left-color:#10B981 !important; }
            [data-ogsc] .av-email-heading, [data-ogsb] .av-email-heading { background-color:#0D1726 !important; color:#F3F6FA !important; }
            [data-ogsc] .av-email-brand, [data-ogsb] .av-email-brand { background-color:#07101F !important; color:#F3F6FA !important; }
            [data-ogsc] .av-email-body, [data-ogsb] .av-email-body { background-color:#0D1726 !important; color:#CBD5E1 !important; }
            [data-ogsc] .av-email-muted, [data-ogsb] .av-email-muted { background-color:#0D1726 !important; color:#AAB4C4 !important; }
            [data-ogsc] .av-email-privacy, [data-ogsb] .av-email-privacy { background-color:#132D2E !important; color:#A7F3D0 !important; }
            [data-ogsc] .av-email-link, [data-ogsb] .av-email-link { background-color:#0D1726 !important; color:#6EE7B7 !important; }
            [data-ogsc] .av-email-button, [data-ogsb] .av-email-button { background-color:#10B981 !important; color:#052E24 !important; }
            </style>
            <!--[if mso]>
            <xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml>
            <![endif]-->
            </head>
            <body class="av-email-page" bgcolor="#F3F8F6" style="margin:0;padding:0;background-color:#F3F8F6;color:#152033;font-family:Arial,Helvetica,sans-serif">
            <div style="display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all">{{E(title)}} — {{E(expiry)}}</div>
            <table class="av-email-page" role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" bgcolor="#F3F8F6" style="width:100%;background-color:#F3F8F6;border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt">
            <tr><td align="center" style="padding:32px 16px">
            <!--[if mso]><table role="presentation" width="560" border="0" cellspacing="0" cellpadding="0"><tr><td><![endif]-->
            <table class="av-email-shell" role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" style="width:100%;max-width:560px;table-layout:fixed;border-collapse:separate;border-spacing:0;mso-table-lspace:0pt;mso-table-rspace:0pt">
            <tr><td style="padding:0 0 24px">
            <table role="presentation" border="0" cellspacing="0" cellpadding="0" style="border-collapse:separate;border-spacing:0"><tr>
            <td bgcolor="#233044" style="background-color:#233044;border-radius:12px;padding:8px"><img src="{{logo}}" width="30" height="40" alt="" style="display:block;border:0"></td>
            <td class="av-email-brand" bgcolor="#F3F8F6" style="padding-left:12px;background-color:#F3F8F6;color:#233044;font-family:Arial,Helvetica,sans-serif;font-size:26px;line-height:34px;font-weight:bold">Averio</td>
            </tr></table>
            </td></tr>
            <tr><td class="av-email-card" bgcolor="#FFFFFF" style="background-color:#FFFFFF;color:#152033;border:1px solid #E4E7EC;border-left:4px solid #10B981;border-radius:18px;padding:28px 20px">
            <table class="av-email-content" role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" style="width:100%;table-layout:fixed;border-collapse:separate;border-spacing:0">
            <tr><td class="av-email-heading" bgcolor="#FFFFFF" style="background-color:#FFFFFF;color:#152033;font-family:Arial,Helvetica,sans-serif">
            <h1 style="margin:0;font-size:26px;line-height:34px;mso-line-height-rule:at-least;font-weight:bold">{{E(title)}}</h1>
            </td></tr>
            <tr><td class="av-email-card" height="18" bgcolor="#FFFFFF" style="height:18px;background-color:#FFFFFF;font-size:1px;line-height:18px">&nbsp;</td></tr>
            <tr><td class="av-email-body" bgcolor="#FFFFFF" style="background-color:#FFFFFF;color:#475467;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:27px">{{E(body)}}</td></tr>
            <tr><td class="av-email-card" height="24" bgcolor="#FFFFFF" style="height:24px;background-color:#FFFFFF;font-size:1px;line-height:24px">&nbsp;</td></tr>
            <tr><td>
            <!--[if mso]>
            <v:roundrect href="{{link}}" style="height:48px;v-text-anchor:middle;width:260px;" arcsize="23%" stroke="f" fillcolor="#10B981">
            <w:anchorlock/>
            <center style="color:#052E24;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:bold">{{E(button)}}</center>
            </v:roundrect>
            <![endif]-->
            <!--[if !mso]><!-->
            <table role="presentation" width="100%" border="0" cellspacing="0" cellpadding="0" style="width:100%;max-width:260px;border-collapse:separate;border-spacing:0"><tr>
            <td class="av-email-button" height="48" align="center" valign="middle" bgcolor="#10B981" style="height:48px;background-color:#10B981;color:#052E24;border:1px solid #10B981;border-radius:11px;font-family:Arial,Helvetica,sans-serif;font-size:16px;line-height:48px;font-weight:bold">
            <a class="av-email-action av-email-button" href="{{link}}" style="display:block;line-height:48px;color:#052E24;font-family:Arial,Helvetica,sans-serif;font-size:16px;font-weight:bold;text-align:center;text-decoration:none">{{E(button)}}</a>
            </td></tr></table>
            <!--<![endif]-->
            </td></tr>
            <tr><td class="av-email-card" height="24" bgcolor="#FFFFFF" style="height:24px;background-color:#FFFFFF;font-size:1px;line-height:24px">&nbsp;</td></tr>
            <tr><td class="av-email-privacy" bgcolor="#ECFDF5" style="padding:16px;background-color:#ECFDF5;color:#065F46;border-radius:12px;font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:24px">{{E(privacy)}}</td></tr>
            <tr><td class="av-email-muted av-email-expiry" bgcolor="#FFFFFF" style="padding-top:20px;background-color:#FFFFFF;color:#667085;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:22px">{{E(expiry)}}</td></tr>
            <tr><td class="av-email-muted" bgcolor="#FFFFFF" style="padding-top:16px;padding-bottom:6px;background-color:#FFFFFF;color:#667085;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px">{{E(fallback)}}</td></tr>
            <tr><td class="av-email-link" bgcolor="#FFFFFF" style="background-color:#FFFFFF;color:#047857;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px;word-wrap:break-word;word-break:break-all;overflow-wrap:anywhere">
            <a class="av-email-fallback av-email-link" href="{{link}}" style="color:#047857;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px;text-decoration:underline;word-wrap:break-word;word-break:break-all;overflow-wrap:anywhere">{{visibleLink}}</a>
            </td></tr>
            </table>
            </td></tr>
            <tr><td class="av-email-footer" bgcolor="#F3F8F6" style="padding:22px 8px;background-color:#F3F8F6;color:#667085;text-align:center;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:20px">{{E(footer)}}</td></tr>
            </table>
            <!--[if mso]></td></tr></table><![endif]-->
            </td></tr></table>
            </body></html>
            """;
    }
}
