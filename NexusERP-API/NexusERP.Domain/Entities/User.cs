namespace NexusERP.Domain.Entities;

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Null until the user creates or is assigned to a company.</summary>
    public string? CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "admin";
}
