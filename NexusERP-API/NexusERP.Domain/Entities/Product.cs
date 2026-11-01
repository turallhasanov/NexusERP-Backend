namespace NexusERP.Domain.Entities;

public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: MHS-001, MHS-002, ...</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Unique per company. Auto-generated as an in-store EAN-13 when not supplied.</summary>
    public string Barcode { get; set; } = string.Empty;

    public decimal PurchasePrice { get; set; }
    public decimal SalePrice { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Site path of the product photo (/uploads/products/{file}), served by this API; null when it has none.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>When true the product is also sent to the Khazar online store, where customers can buy it.</summary>
    public bool ShowOnWebsite { get; set; }

    public Category? Category { get; set; }
    public Unit? Unit { get; set; }
}
