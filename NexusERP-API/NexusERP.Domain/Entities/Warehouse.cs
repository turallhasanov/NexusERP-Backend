namespace NexusERP.Domain.Entities;

public class Warehouse
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: DPO-001, DPO-002, ...</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}
