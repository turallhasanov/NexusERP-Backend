using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>One-time 6-digit code sent by e-mail. Only a hash of the code is stored.</summary>
public class OtpCode
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Trimmed, lower-case, like <see cref="User.Email"/>.</summary>
    public string Email { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }

    /// <summary>SHA-256 of "{Id}:{code}", hex.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the code is accepted, replaced by a newer one, or burned by too many wrong attempts.</summary>
    public bool IsUsed { get; set; }

    /// <summary>Wrong guesses so far; the code is burned at the limit.</summary>
    public int FailedAttempts { get; set; }
}
