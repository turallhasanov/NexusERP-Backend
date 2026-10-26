namespace NexusERP.Domain.Entities;

public class Warehouse
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: DPO-001, DPO-002, ...</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }

    /// <summary>Person in charge of the warehouse (məsul şəxs), free text.</summary>
    public string? ResponsiblePerson { get; set; }

    /// <summary>Planned capacity in stock units (tutum); informational, not enforced on receipts.</summary>
    public decimal? Capacity { get; set; }

    /// <summary>False once deleted while documents still reference it; hidden from lists and new documents.</summary>
    public bool IsActive { get; set; } = true;
}
