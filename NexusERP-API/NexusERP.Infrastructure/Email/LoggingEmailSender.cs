using Microsoft.Extensions.Logging;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Infrastructure.Email;

/// <summary>
/// Used in Development when no SMTP server is configured: writes the message (including any code) to the
/// log instead of sending it, so sign-up and password reset can be tried without a mail account.
/// Never registered outside Development, where codes in logs would be a leak.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "SMTP is not configured (\"Email:Host\" is empty); e-mail NOT sent. To: {To} | Subject: {Subject} | {Body}",
            message.To,
            message.Subject,
            message.TextBody);

        return Task.CompletedTask;
    }
}

/// <summary>Outside Development with no SMTP server: every send fails, so the API answers 503 instead of
/// pretending a code was delivered.</summary>
public class UnconfiguredEmailSender : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("SMTP is not configured: set the \"Email\" section (Host, Port, ...) in appsettings.");
}
