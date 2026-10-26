using System.Net;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Auth;

/// <summary>Azerbaijani e-mails carrying a one-time code, as HTML with a plain-text alternative.</summary>
internal static class OtpEmails
{
    private const string Subject = "NexusERP - Təhlükəsizlik Kodunuz";
    private const string Title = "Təsdiqləmə Kodu";
    private const string Intro = "NexusERP hesabınıza daxil olmaq və ya əməliyyatı təsdiqləmək üçün aşağıdakı birdəfəlik təhlükəsizlik kodundan (OTP) istifadə edin:";
    private const string Ignore = "Bu sorğunu siz göndərməmisinizsə, bu məktubu nəzərə almayın.";

    public static EmailMessage Build(string to, OtpPurpose purpose, string code, TimeSpan lifetime)
    {
        var minutes = (int)lifetime.TotalMinutes;
        var year = DateTime.UtcNow.Year;

        var purposeLabel = purpose == OtpPurpose.PasswordReset ? "Şifrə bərpası" : "E-poçt təsdiqi";
        var warning = $"Bu kod {minutes} dəqiqə ərzində etibarlıdır. Təhlükəsizliyiniz üçün bu kodu heç kimlə paylaşmayın.";
        var footer = $"© {year} NexusERP. Bütün hüquqlar qorunur. Bu avtomatik göndərilən e-poçtdur, cavab yazmayın.";

        var text = $"""
            {Title} ({purposeLabel})

            {Intro}

            Kod: {code}

            {warning}
            {Ignore}

            {footer}
            """;

        return new EmailMessage(to, Subject, BuildHtml(purposeLabel, code, warning, footer, minutes), text);
    }

    // Table layout with inline styles only, so Outlook, Gmail and Apple Mail render it the same way. Solid
    // colours throughout (no gradients or shadows) keep the look identical in clients that strip modern CSS.
    // The code stays one unbroken string (spaced with letter-spacing), so copying it pastes cleanly; the extra
    // left padding balances the trailing letter-spacing after the last digit.
    private static string BuildHtml(string purposeLabel, string code, string warning, string footer, int minutes)
    {
        var preheader = WebUtility.HtmlEncode($"{Title} - {minutes} dəqiqə etibarlıdır.");

        return $"""
            <!DOCTYPE html>
            <html lang="az" xmlns="http://www.w3.org/1999/xhtml">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta name="color-scheme" content="dark">
              <meta name="supported-color-schemes" content="dark">
              <title>{WebUtility.HtmlEncode(Subject)}</title>
            </head>
            <body style="margin:0;padding:0;background-color:#0b0f17;">
              <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:#0b0f17;">{preheader}</div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#0b0f17" style="background-color:#0b0f17;">
                <tr>
                  <td align="center" style="padding:40px 16px;">
                    <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:520px;">
                      <tr>
                        <td style="padding:0 0 20px;font-family:Arial,Helvetica,sans-serif;font-size:18px;font-weight:bold;letter-spacing:0.5px;color:#e2e8f0;">Nexus<span style="color:#7dd3fc;">ERP</span></td>
                      </tr>
                      <tr>
                        <td bgcolor="#0f172a" style="background-color:#0f172a;border:1px solid #1e293b;border-radius:12px;padding:32px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td style="font-family:Arial,Helvetica,sans-serif;font-size:12px;font-weight:bold;letter-spacing:1px;text-transform:uppercase;color:#64748b;padding:0 0 8px;">{WebUtility.HtmlEncode(purposeLabel)}</td>
                            </tr>
                            <tr>
                              <td style="font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:bold;color:#f1f5f9;padding:0 0 12px;">{WebUtility.HtmlEncode(Title)}</td>
                            </tr>
                            <tr>
                              <td style="font-family:Arial,Helvetica,sans-serif;font-size:15px;line-height:24px;color:#94a3b8;padding:0 0 24px;">{WebUtility.HtmlEncode(Intro)}</td>
                            </tr>
                            <tr>
                              <td align="center" style="padding:0 0 24px;">
                                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                                  <tr>
                                    <td align="center" bgcolor="#111c2e" style="background-color:#111c2e;border:1px solid #1e293b;border-radius:8px;padding:18px 16px 18px 26px;font-family:'Courier New',Consolas,Menlo,monospace;font-size:32px;font-weight:bold;letter-spacing:10px;color:#a5f3fc;">{code}</td>
                                  </tr>
                                </table>
                              </td>
                            </tr>
                            <tr>
                              <td style="border-left:3px solid #d97706;padding:2px 0 2px 12px;font-family:Arial,Helvetica,sans-serif;font-size:13px;line-height:20px;color:#cbd5e1;">{WebUtility.HtmlEncode(warning)}</td>
                            </tr>
                            <tr>
                              <td style="font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#64748b;padding:20px 0 0;">{WebUtility.HtmlEncode(Ignore)}</td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td align="center" style="padding:20px 16px 0;font-family:Arial,Helvetica,sans-serif;font-size:12px;line-height:18px;color:#475569;">{WebUtility.HtmlEncode(footer)}</td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
