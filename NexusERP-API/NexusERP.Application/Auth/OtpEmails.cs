using System.Net;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Auth;

/// <summary>Azerbaijani e-mails carrying a one-time code, as HTML with a plain-text alternative.</summary>
internal static class OtpEmails
{
    public static EmailMessage Build(string to, OtpPurpose purpose, string code, TimeSpan lifetime)
    {
        var minutes = (int)lifetime.TotalMinutes;

        var (subject, intro) = purpose == OtpPurpose.PasswordReset
            ? ("NexusERP: şifrə bərpası kodu", "Şifrənizi yeniləmək üçün aşağıdakı kodu daxil edin.")
            : ("NexusERP: e-poçt təsdiq kodu", "Qeydiyyatı tamamlamaq üçün aşağıdakı kodu daxil edin.");

        const string ignore = "Bu sorğunu siz göndərməmisinizsə, bu məktubu nəzərə almayın.";

        var text = $"""
            {intro}

            Kod: {code}

            Kod {minutes} dəqiqə etibarlıdır.
            {ignore}

            NexusERP
            """;

        var html = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:0 auto;color:#171717">
              <p style="font-size:12px;letter-spacing:4px;color:#a3a3a3;margin:0 0 16px">NEXUSERP</p>
              <p style="font-size:15px;margin:0 0 20px">{WebUtility.HtmlEncode(intro)}</p>
              <p style="font-size:32px;font-weight:bold;letter-spacing:8px;margin:0 0 20px;font-family:Consolas,monospace">{code}</p>
              <p style="font-size:13px;color:#525252;margin:0 0 8px">Kod {minutes} dəqiqə etibarlıdır.</p>
              <p style="font-size:13px;color:#525252;margin:0">{WebUtility.HtmlEncode(ignore)}</p>
            </div>
            """;

        return new EmailMessage(to, subject, html, text);
    }
}
