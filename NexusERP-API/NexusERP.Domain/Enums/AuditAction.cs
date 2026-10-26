namespace NexusERP.Domain.Enums;

public enum AuditAction
{
    Create = 1,

    /// <summary>A change to an existing record, including status changes (approve, terminate, deactivate).</summary>
    Update = 2,

    Delete = 3
}
