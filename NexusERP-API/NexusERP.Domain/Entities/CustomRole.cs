namespace NexusERP.Domain.Entities;

/// <summary>
/// A job role a company defines for its users (e.g. "Anbardar", "Maliyyəçi"). It sits beside the user's
/// access level in <see cref="User.Role"/> (admin, itadmin, staff), which it never changes.
/// </summary>
public class CustomRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Unique within the company.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Module keys from <c>AppPermissions.All</c> (inventory, sales, ...); stored as a JSON array.</summary>
    public List<string> Permissions { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
