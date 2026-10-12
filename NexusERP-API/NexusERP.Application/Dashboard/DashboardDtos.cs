using NexusERP.Application.Common;

namespace NexusERP.Application.Dashboard;

/// <summary>
/// Company-wide totals. Sales and purchases count approved documents only; products count active ones.
/// A product is low on stock when its quantity summed over all warehouses is at or below the threshold
/// (products that were never stocked count as 0).
/// </summary>
public sealed record DashboardSummaryDto(
    decimal TotalSales,
    decimal TotalPurchases,
    int TotalProducts,
    int LowStockProductsCount,
    decimal LowStockThreshold);

/// <summary>GET /api/dashboard/summary: { "ok": true, "summary": { ... } }</summary>
public sealed record DashboardSummaryResponseDto(DashboardSummaryDto Summary) : SuccessResponse;
