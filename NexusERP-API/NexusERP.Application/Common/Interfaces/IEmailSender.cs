namespace NexusERP.Application.Common.Interfaces;

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string TextBody);

/// <summary>Sends transactional e-mail. Throws when the message could not be handed to the mail server.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
