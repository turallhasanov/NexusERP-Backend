using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>Sale (satış): goods sold in a store, shipped from a warehouse.</summary>
public class Sale
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>Auto-generated per company: STŞ-001, STŞ-002, ...</summary>
    public string DocumentNumber { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    /// <summary>Warehouse the stock is taken from; defaults to the store's warehouse.</summary>
    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>Null for POS walk-in customers.</summary>
    public string? CustomerId { get; set; }

    public DateTime DocumentDate { get; set; }

    /// <summary>Sum of the items' <see cref="SaleItem.TotalPrice"/>.</summary>
    public decimal TotalAmount { get; set; }

    public SaleStatus Status { get; set; } = SaleStatus.Draft;

    public SaleChannel Channel { get; set; } = SaleChannel.Direct;

    /// <summary>The online store's order number (e.g. KHZ-7Q2MX); unique per company, so a resent order is not duplicated.</summary>
    public string? ExternalOrderNumber { get; set; }

    /// <summary>
    /// The end customer of an online order (pərakəndə müştəri bilgiləri). <see cref="CustomerId"/> then points at the
    /// company's fixed "Khazar Online Müştəri" account, which carries the balance.
    /// </summary>
    public string? RetailCustomerName { get; set; }
    public string? RetailCustomerPhone { get; set; }
    public string? RetailCustomerEmail { get; set; }
    public string? RetailCustomerAddress { get; set; }

    public Store? Store { get; set; }
    public Warehouse? Warehouse { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
}
