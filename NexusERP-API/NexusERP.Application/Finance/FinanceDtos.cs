using NexusERP.Application.Common;

namespace NexusERP.Application.Finance;

/// <summary>
/// Profit and loss for one period. Revenue is approved sales, split by kind: POS (walk-in), wholesale (with a
/// kontragent) and Khazar online. Costs are the purchase cost of the goods sold (cost of goods sold, at each product's
/// current purchase price), operating expenses and salaries. <c>profitMargin</c> is net profit as a percentage of
/// revenue, null when there is no revenue.
/// </summary>
public sealed record FinanceTotalsDto(
    decimal PosRevenue,
    decimal WholesaleRevenue,
    decimal OnlineRevenue,
    decimal Revenue,
    decimal CostOfGoodsSold,
    decimal OperatingExpenses,
    decimal Payroll,
    decimal TotalExpenses,
    decimal NetProfit,
    decimal? ProfitMargin);

/// <summary>One row of the store breakdown. <c>storeId</c> null is the "not assigned to a store" row.</summary>
public sealed record StoreFinanceRowDto(string? StoreId, string Name, FinanceTotalsDto Totals);

/// <summary>
/// GET /api/finance/summary → { "ok": true, "period", "from", "to", "storeId", "totals", "stores": [ ... ] }.
/// <c>from</c>/<c>to</c> are the period's bounds in UTC (<c>to</c> exclusive). With a store, totals and the single
/// row are that store's; without, totals cover the whole company and the rows break it down by store.
/// </summary>
public sealed record FinanceSummaryResponseDto(
    string Period,
    DateTime From,
    DateTime To,
    string? StoreId,
    FinanceTotalsDto Totals,
    IReadOnlyList<StoreFinanceRowDto> Stores) : SuccessResponse;
