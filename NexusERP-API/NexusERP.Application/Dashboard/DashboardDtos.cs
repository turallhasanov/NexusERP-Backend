using NexusERP.Application.Common;

namespace NexusERP.Application.Dashboard;

/// <summary>
/// Company totals for an optional date range (<c>from</c>/<c>to</c> echo it; null means unbounded).
/// Sales and purchases count approved documents only; <c>totalSales</c>, <c>cogs</c>, <c>totalExpenses</c> and
/// <c>netProfit</c> are the same figures as GET /api/reports/profit-loss for that range. Products and low stock
/// are current state, not limited by the range: a product is low on stock when its quantity summed over all
/// warehouses is at or below the threshold (products that were never stocked count as 0).
/// </summary>
public sealed record DashboardSummaryDto(
    DateTime? From,
    DateTime? To,
    decimal TotalSales,
    decimal Cogs,
    decimal GrossProfit,
    decimal TotalExpenses,
    decimal NetProfit,
    decimal TotalPurchases,
    int TotalProducts,
    int LowStockProductsCount,
    decimal LowStockThreshold);

/// <summary>GET /api/dashboard/summary: { "ok": true, "summary": { ... } }</summary>
public sealed record DashboardSummaryResponseDto(DashboardSummaryDto Summary) : SuccessResponse;
