namespace NexusERP.Infrastructure.Authentication;

public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    /// <summary>HMAC-SHA256 signing key; must be at least 32 bytes.</summary>
    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 60;

    /// <summary>Lifetime of the read-only tokens platform administrators get to view a company.</summary>
    public int ImpersonationExpiryMinutes { get; init; } = 60;
}
