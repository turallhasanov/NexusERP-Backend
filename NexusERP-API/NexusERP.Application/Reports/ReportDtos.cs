using NexusERP.Application.Common;

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

/// <summary>
/// GET /api/reports/profit-loss → { "ok": true, "from", "to", "grossRevenue", "cogs", "grossProfit", "totalExpenses", "netProfit" }.
/// <c>from</c>/<c>to</c> echo the requested range; null means unbounded.
/// </summary>
public sealed record ProfitLossResponseDto(
    DateTime? From,
    DateTime? To,
    decimal GrossRevenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal TotalExpenses,
    decimal NetProfit) : SuccessResponse;

/// <summary>Row in GET /api/reports/top-products: totals over approved sales.</summary>
public sealed record TopProductReportDto(
    string ProductId,
    string ProductName,
    decimal QuantitySold,
    decimal Revenue);
