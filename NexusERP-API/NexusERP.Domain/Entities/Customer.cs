namespace NexusERP.Domain.Entities;

public class Customer
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: KNT-001, KNT-002, ...</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>VÖEN — Azerbaijani taxpayer identification number.</summary>
    public string? Voen { get; set; }

    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
