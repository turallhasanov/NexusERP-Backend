namespace NexusERP.Domain.Entities;

/// <summary>Current balance (stok qalığı) of one product in one warehouse.</summary>
public class Stock
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;
    public string WarehouseId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }

    public Warehouse? Warehouse { get; set; }
    public Product? Product { get; set; }
}
