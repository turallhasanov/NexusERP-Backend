using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Reports;

/// <summary>
/// Date range of a report. Document dates are stored in UTC, so bounds are compared in UTC.
/// <c>EndExclusive</c> is used for a date-only <c>to</c> (the whole day), <c>EndInclusive</c> for a timestamp.
/// </summary>
internal readonly record struct ReportRange(DateTime? Start, DateTime? EndInclusive, DateTime? EndExclusive)
{
    public IQueryable<Sale> Apply(IQueryable<Sale> sales)
    {
        if (Start is { } start)
            sales = sales.Where(s => s.DocumentDate >= start);

        if (EndExclusive is { } exclusive)
            sales = sales.Where(s => s.DocumentDate < exclusive);
        else if (EndInclusive is { } inclusive)
            sales = sales.Where(s => s.DocumentDate <= inclusive);

        return sales;
    }

    public IQueryable<Purchase> Apply(IQueryable<Purchase> purchases)
    {
        if (Start is { } start)
            purchases = purchases.Where(p => p.DocumentDate >= start);

        if (EndExclusive is { } exclusive)
            purchases = purchases.Where(p => p.DocumentDate < exclusive);
        else if (EndInclusive is { } inclusive)
            purchases = purchases.Where(p => p.DocumentDate <= inclusive);

        return purchases;
    }

    public IQueryable<Expense> Apply(IQueryable<Expense> expenses)
    {
        if (Start is { } start)
            expenses = expenses.Where(e => e.ExpenseDate >= start);

        if (EndExclusive is { } exclusive)
            expenses = expenses.Where(e => e.ExpenseDate < exclusive);
        else if (EndInclusive is { } inclusive)
            expenses = expenses.Where(e => e.ExpenseDate <= inclusive);

        return expenses;
    }
}

internal readonly record struct ProfitLossTotals(decimal GrossRevenue, decimal Cogs, decimal TotalExpenses)
{
    public decimal GrossProfit => GrossRevenue - Cogs;
    public decimal NetProfit => GrossProfit - TotalExpenses;
}

/// <summary>Profit and loss figures shared by GET /api/reports/profit-loss and the dashboard summary.</summary>
internal static class ProfitLoss
{
    public const string InvalidRangeMessage = "Başlanğıc tarixi son tarixdən sonra ola bilməz.";

    /// <summary>
    /// A value with an offset ("…Z", "…+04:00") binds as Local time; it is converted to UTC. A date-only
    /// <paramref name="to"/> (no zone, midnight) means that whole day, so ?to=2026-10-31 includes the 31st.
    /// Returns null when <paramref name="from"/> is after <paramref name="to"/>.
    /// </summary>
    public static ReportRange? ToRange(DateTime? from, DateTime? to)
    {
        var start = ToUtc(from);
        var end = ToUtc(to);

        if (start > end)
            return null;

        var isWholeDay = to is { Kind: DateTimeKind.Unspecified, TimeOfDay.Ticks: 0 } day && day < DateTime.MaxValue.Date;

        return isWholeDay
            ? new ReportRange(start, null, to!.Value.AddDays(1))
            : new ReportRange(start, end, null);
    }

    public static DateTime? ToUtc(DateTime? value) =>
        value is { Kind: DateTimeKind.Local } local ? local.ToUniversalTime() : value;

    public static async Task<ProfitLossTotals> ComputeAsync(IApplicationDbContext db, string companyId, ReportRange range, CancellationToken cancellationToken)
    {
        // Only approved sales: drafts are not revenue yet.
        var sales = range.Apply(db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.Status == SaleStatus.Approved));

        var expenses = range.Apply(db.Expenses
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId));

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

        return new ProfitLossTotals(grossRevenue, cogs, totalExpenses);
    }
}
