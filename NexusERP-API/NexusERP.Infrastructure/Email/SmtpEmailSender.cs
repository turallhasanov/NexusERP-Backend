using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Infrastructure.Email;

/// <summary>Sends through the SMTP server in the "Email" settings, one connection per message.</summary>
public class SmtpEmailSender(IOptions<EmailSettings> options) : IEmailSender
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        var security = !_settings.EnableSsl
            ? SecureSocketOptions.None
            : _settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var client = new SmtpClient { Timeout = 15_000 };

        await client.ConnectAsync(_settings.Host, _settings.Port, security, cancellationToken);

        if (!string.IsNullOrEmpty(_settings.Username))
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
