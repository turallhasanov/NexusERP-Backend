namespace NexusERP.Application.Reports;

/// <summary>
/// Row in GET /api/reports/inventory-value: stock held in one warehouse, valued at each product's
/// current purchase price.
/// </summary>
public sealed record InventoryReportDto(
    string WarehouseId,
    string WarehouseName,
    decimal TotalStockQuantity,
    decimal TotalStockValue);

/// <summary>Row in GET /api/reports/top-products: totals over approved sales.</summary>
public sealed record TopProductReportDto(
    string ProductId,
    string ProductName,
    decimal QuantitySold,
    decimal Revenue);
