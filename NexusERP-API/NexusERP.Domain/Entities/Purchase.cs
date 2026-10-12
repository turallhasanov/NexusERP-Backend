using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>Purchase receipt (anbara mədaxil): goods bought from a supplier into a warehouse.</summary>
public class Purchase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: ALS-001, ALS-002, ...</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>The supplier (kontragent) the goods are bought from.</summary>
    public string CustomerId { get; set; } = string.Empty;

    public DateTime DocumentDate { get; set; }

    /// <summary>Sum of the items' <see cref="PurchaseItem.TotalPrice"/>.</summary>
    public decimal TotalAmount { get; set; }

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Draft;

    public Warehouse? Warehouse { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
}
