namespace NexusERP.Domain.Entities;

public class SaleItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SaleId { get; set; } = string.Empty;

    /// <summary>1-based position in the document, so lines come back in the order they were entered.</summary>
    public int LineNumber { get; set; }

    public string ProductId { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>Quantity × UnitPrice, rounded to 2 decimals.</summary>
    public decimal TotalPrice { get; set; }

    public Product? Product { get; set; }
}
