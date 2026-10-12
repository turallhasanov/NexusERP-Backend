namespace NexusERP.Infrastructure.Email;

/// <summary>"Email" section of appsettings. An empty <see cref="Host"/> means SMTP is not configured.</summary>
public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    /// <summary>587 (STARTTLS) or 465 (SSL from the start); 25 or a local test port with EnableSsl = false.</summary>
    public int Port { get; set; } = 587;

    /// <summary>true: encrypted (SSL on 465, STARTTLS on any other port). false: plain text, local testing only.</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>Empty to send without logging in (local relay).</summary>
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "NexusERP";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);
}
