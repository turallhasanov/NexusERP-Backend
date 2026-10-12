using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Reports;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Dashboard;

public class DashboardService(IApplicationDbContext db) : IDashboardService
{
    public const decimal DefaultLowStockThreshold = 5m;

    // Upper bound of the decimal(18,3) quantity column.
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    public async Task<Result<DashboardSummaryResponseDto>> GetSummaryAsync(
        string companyId, decimal? lowStockThreshold, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        // Rounded to the column scale, so the comparison matches what is stored.
        var threshold = Math.Round(lowStockThreshold ?? DefaultLowStockThreshold, 3, MidpointRounding.AwayFromZero);

        if (threshold < 0)
            return Result<DashboardSummaryResponseDto>.Failure(ErrorType.Validation, "Minimum qalıq həddi mənfi ola bilməz.");

        if (threshold > MaxQuantity)
            return Result<DashboardSummaryResponseDto>.Failure(ErrorType.Validation, "Minimum qalıq həddi həddindən artıq böyükdür.");

        if (ProfitLoss.ToRange(from, to) is not { } range)
            return Result<DashboardSummaryResponseDto>.Failure(ErrorType.Validation, ProfitLoss.InvalidRangeMessage);

        // Each total is one aggregate query run in SQL; nothing is loaded into memory.
        // Run one after another: a DbContext does not support parallel queries.
        var profitLoss = await ProfitLoss.ComputeAsync(db, companyId, range, cancellationToken);

        var totalPurchases = await range.Apply(db.Purchases
                .AsNoTracking()
                .Where(p => p.CompanyId == companyId && p.Status == PurchaseStatus.Approved))
            .SumAsync(p => p.TotalAmount, cancellationToken);

        var activeProducts = db.Products
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && p.IsActive);

        var totalProducts = await activeProducts.CountAsync(cancellationToken);

        var lowStockProductsCount = await activeProducts
            .CountAsync(p => (db.Stocks
                .Where(s => s.CompanyId == companyId && s.ProductId == p.Id)
                .Sum(s => (decimal?)s.Quantity) ?? 0) <= threshold,
                cancellationToken);

        return new DashboardSummaryResponseDto(new DashboardSummaryDto(
            ProfitLoss.ToUtc(from),
            ProfitLoss.ToUtc(to),
            profitLoss.GrossRevenue,
            profitLoss.Cogs,
            profitLoss.GrossProfit,
            profitLoss.TotalExpenses,
            profitLoss.NetProfit,
            totalPurchases,
            totalProducts,
            lowStockProductsCount,
            threshold));
    }
}
