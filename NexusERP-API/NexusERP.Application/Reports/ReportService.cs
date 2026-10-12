using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Reports;

public class ReportService(IApplicationDbContext db) : IReportService
{
    public const int DefaultTopProductsLimit = 10;
    public const int MaxTopProductsLimit = 100;

    private const string SortByQuantity = "quantity";
    private const string SortByRevenue = "revenue";

    public async Task<Result<ItemsResponseDto<InventoryReportDto>>> GetInventoryValueAsync(string companyId, CancellationToken cancellationToken)
    {
        // Active warehouses are always listed (empty ones with zeros); inactive ones only while they still hold stock.
        var rows = await db.Warehouses
            .AsNoTracking()
            .Where(w => w.CompanyId == companyId
                && (w.IsActive || db.Stocks.Any(s => s.CompanyId == companyId && s.WarehouseId == w.Id && s.Quantity != 0)))
            .OrderBy(w => w.Name)
            .Select(w => new
            {
                w.Id,
                w.Name,
                Quantity = db.Stocks
                    .Where(s => s.CompanyId == companyId && s.WarehouseId == w.Id)
                    .Sum(s => (decimal?)s.Quantity) ?? 0,
                Value = db.Stocks
                    .Where(s => s.CompanyId == companyId && s.WarehouseId == w.Id)
                    .Sum(s => (decimal?)(s.Quantity * s.Product!.PurchasePrice)) ?? 0
            })
            .ToListAsync(cancellationToken);

        // quantity × price has 5 decimals in SQL; money goes out with 2.
        var items = rows
            .Select(r => new InventoryReportDto(
                r.Id,
                r.Name,
                r.Quantity,
                Math.Round(r.Value, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new ItemsResponseDto<InventoryReportDto>(items);
    }

    public async Task<Result<ItemsResponseDto<TopProductReportDto>>> GetTopProductsAsync(string companyId, int? limit, string? sortBy, CancellationToken cancellationToken)
    {
        var take = limit ?? DefaultTopProductsLimit;

        if (take < 1 || take > MaxTopProductsLimit)
            return Result<ItemsResponseDto<TopProductReportDto>>.Failure(
                ErrorType.Validation, $"Limit 1 ilə {MaxTopProductsLimit} arasında olmalıdır.");

        var order = string.IsNullOrWhiteSpace(sortBy) ? SortByQuantity : sortBy.Trim().ToLowerInvariant();

        if (order is not (SortByQuantity or SortByRevenue))
            return Result<ItemsResponseDto<TopProductReportDto>>.Failure(
                ErrorType.Validation, "Sıralama yalnız 'quantity' və ya 'revenue' ola bilər.");

        // Only approved sales: drafts have not left the warehouse yet.
        var totals = db.SaleItems
            .AsNoTracking()
            .Where(i => db.Sales.Any(s => s.Id == i.SaleId && s.CompanyId == companyId && s.Status == SaleStatus.Approved))
            .GroupBy(i => new { i.ProductId, i.Product!.Name })
            // Anonymous type, not the DTO: EF cannot sort on members of a constructor-projected record.
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.Name,
                Quantity = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.TotalPrice)
            });

        // The other measure breaks ties, then the name, so equal rows come back in a stable order.
        var ordered = order == SortByRevenue
            ? totals.OrderByDescending(t => t.Revenue).ThenByDescending(t => t.Quantity)
            : totals.OrderByDescending(t => t.Quantity).ThenByDescending(t => t.Revenue);

        var rows = await ordered
            .ThenBy(t => t.Name).ThenBy(t => t.ProductId)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new TopProductReportDto(r.ProductId, r.Name, r.Quantity, r.Revenue))
            .ToList();

        return new ItemsResponseDto<TopProductReportDto>(items);
    }

    public async Task<Result<ProfitLossResponseDto>> GetProfitLossAsync(string companyId, DateTime? from, DateTime? to, CancellationToken cancellationToken)
    {
        if (from > to)
            return Result<ProfitLossResponseDto>.Failure(ErrorType.Validation, "Başlanğıc tarixi son tarixdən sonra ola bilməz.");

        // A date-only "to" (midnight) means the whole day, so ?to=2026-10-31 includes sales made on the 31st.
        DateTime? end = to is { TimeOfDay.Ticks: 0 } day && day < DateTime.MaxValue.Date ? day.AddDays(1) : null;

        // Only approved sales: drafts are not revenue yet.
        var sales = db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.Status == SaleStatus.Approved);

        var expenses = db.Expenses
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId);

        if (from is { } start)
        {
            sales = sales.Where(s => s.DocumentDate >= start);
            expenses = expenses.Where(e => e.ExpenseDate >= start);
        }

        if (end is { } exclusiveEnd)
        {
            sales = sales.Where(s => s.DocumentDate < exclusiveEnd);
            expenses = expenses.Where(e => e.ExpenseDate < exclusiveEnd);
        }
        else if (to is { } inclusiveEnd)
        {
            sales = sales.Where(s => s.DocumentDate <= inclusiveEnd);
            expenses = expenses.Where(e => e.ExpenseDate <= inclusiveEnd);
        }

        // Each total is one aggregate query in SQL. Run one after another: a DbContext does not support parallel queries.
        var grossRevenue = await sales.SumAsync(s => s.TotalAmount, cancellationToken);

        // Cost is valued at each product's current purchase price, as with the inventory report.
        // quantity × price has 5 decimals in SQL; money goes out with 2.
        var cogs = Math.Round(
            await db.SaleItems
                .Where(i => sales.Any(s => s.Id == i.SaleId))
                .SumAsync(i => i.Quantity * i.Product!.PurchasePrice, cancellationToken),
            2,
            MidpointRounding.AwayFromZero);

        var totalExpenses = await expenses.SumAsync(e => e.Amount, cancellationToken);

        var grossProfit = grossRevenue - cogs;

        return new ProfitLossResponseDto(
            from,
            to,
            grossRevenue,
            cogs,
            grossProfit,
            totalExpenses,
            grossProfit - totalExpenses);
    }
}
