namespace NexusERP.Domain.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Null until the user creates or is assigned to a company.</summary>
    public string? CompanyId { get; set; }

    /// <summary>Full name (ad soyad).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stored trimmed and lower-case; unique.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Identity PBKDF2 hash. Empty only for the built-in demo account, which takes any password.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = "admin";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Inactive users cannot log in.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Set by entering the code e-mailed at registration (or completing a password reset).
    /// Unconfirmed users cannot log in.</summary>
    public bool IsEmailConfirmed { get; set; }
}
