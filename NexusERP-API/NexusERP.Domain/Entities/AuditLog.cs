using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>
/// One critical action (create, change, delete) a user took in a company, for the IT admin's audit trail. The user's
/// name and role are copied at the time, so the entry stays readable after the account changes or is removed.
/// </summary>
public class AuditLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Who did it; null if the request had no signed-in user.</summary>
    public string? UserId { get; set; }

    public string UserName { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;

    /// <summary>The module's department: Maliyyə, İHR, Anbar or Şəbəkə.</summary>
    public string Department { get; set; } = string.Empty;

    public AuditAction ActionType { get; set; }
    public AuditModule ModuleName { get; set; }

    /// <summary>Azerbaijani sentence, e.g. "Əli M. tərəfindən 'ALS-001' alış qaiməsi təsdiqləndi".</summary>
    public string Description { get; set; } = string.Empty;

    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
