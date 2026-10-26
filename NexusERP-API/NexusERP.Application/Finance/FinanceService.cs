using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Finance;

public class FinanceService(IApplicationDbContext db) : IFinanceService
{
    public const string Daily = "daily";
    public const string Monthly = "monthly";
    public const string Yearly = "yearly";

    // Periods are Azerbaijani calendar days, months and years: UTC+4 all year (no daylight saving since 2016).
    private static readonly TimeSpan BakuOffset = TimeSpan.FromHours(4);

    private const string UnassignedName = "Mağazaya aid olmayan";

    private const string InvalidPeriod = "Dövr yanlışdır. Mümkün dəyərlər: daily, monthly, yearly.";
    private const string StoreNotFound = "Mağaza tapılmadı.";

    // How far back the mizan goes, in its own periods.
    private const int MizanDays = 60;
    private const int MizanMonths = 24;
    private const int MizanYears = 10;

    public async Task<Result<FinanceSummaryResponseDto>> GetSummaryAsync(string companyId, string? storeId, string? period, CancellationToken cancellationToken)
    {
        period = NormalizePeriod(period);

        if (period is null)
            return Result<FinanceSummaryResponseDto>.Failure(ErrorType.Validation, InvalidPeriod);

        storeId = string.IsNullOrWhiteSpace(storeId) ? null : storeId.Trim();

        var stores = await db.Stores
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.Code.Length).ThenBy(s => s.Code)
            .Select(s => new { s.Id, s.Name, s.IsActive })
            .ToListAsync(cancellationToken);

        // Another company's store looks exactly like a missing one.
        if (storeId is not null && stores.All(s => s.Id != storeId))
            return Result<FinanceSummaryResponseDto>.Failure(ErrorType.NotFound, StoreNotFound);

        var today = Today;
        var firstDay = SlotStart(today, period);
        var from = ToUtc(firstDay);
        var to = ToUtc(NextSlot(firstDay, period));

        // Only approved sales are revenue; drafts have not moved stock or money yet.
        var sales = db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.Status == SaleStatus.Approved && s.DocumentDate >= from && s.DocumentDate < to);

        var expenses = db.Expenses
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.ExpenseDate >= from && e.ExpenseDate < to);

        if (storeId is not null)
        {
            sales = sales.Where(s => s.StoreId == storeId);
            expenses = expenses.Where(e => e.StoreId == storeId);
        }

        // One grouped query per figure; a DbContext does not run queries in parallel.
        var revenue = await sales
            .GroupBy(s => new { s.StoreId, s.Channel, IsWholesale = s.CustomerId != null })
            .Select(g => new { g.Key.StoreId, g.Key.Channel, g.Key.IsWholesale, Total = g.Sum(s => s.TotalAmount) })
            .ToListAsync(cancellationToken);

        // Cost of goods sold at each product's current purchase price, as in GET /api/reports/profit-loss.
        var cogs = await (
                from item in db.SaleItems
                join sale in sales on item.SaleId equals sale.Id
                group new { item.Quantity, item.Product!.PurchasePrice } by sale.StoreId into g
                select new { StoreId = g.Key, Total = g.Sum(x => x.Quantity * x.PurchasePrice) })
            .ToListAsync(cancellationToken);

        var operating = await expenses
            .GroupBy(e => e.StoreId)
            .Select(g => new { StoreId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && (storeId == null || e.StoreId == storeId))
            .Select(e => new { e.StoreId, e.Salary, e.HiredAt, e.TerminatedAt })
            .ToListAsync(cancellationToken);

        // Buckets per store; "" collects what belongs to no store (company-wide expenses and staff).
        var buckets = new Dictionary<string, Bucket>();
        Bucket For(string? id) => buckets.TryGetValue(id ?? "", out var b) ? b : buckets[id ?? ""] = new Bucket();

        foreach (var row in revenue)
        {
            var bucket = For(row.StoreId);

            if (row.Channel == SaleChannel.Online)
                bucket.Online += row.Total;
            else if (row.IsWholesale)
                bucket.Wholesale += row.Total;
            else
                bucket.Pos += row.Total;
        }

        foreach (var row in cogs)
            For(row.StoreId).Cogs += row.Total;

        foreach (var row in operating)
            For(row.StoreId).Operating += row.Total;

        foreach (var employee in employees)
            For(employee.StoreId).Payroll += PayrollFor(employee.Salary, employee.HiredAt, employee.TerminatedAt, period!, firstDay, today);

        var rows = new List<StoreFinanceRowDto>();

        foreach (var store in stores)
        {
            // A closed store shows only if it still has figures in the period.
            if (storeId is not null ? store.Id != storeId : !store.IsActive && !buckets.ContainsKey(store.Id))
                continue;

            rows.Add(new StoreFinanceRowDto(store.Id, store.Name, For(store.Id).ToDto()));
        }

        if (storeId is null && buckets.TryGetValue("", out var unassigned))
            rows.Add(new StoreFinanceRowDto(null, UnassignedName, unassigned.ToDto()));

        var totals = buckets.Values.Aggregate(new Bucket(), (sum, b) => sum.Add(b)).ToDto();

        return new FinanceSummaryResponseDto(period!, from, to, storeId, totals, rows);
    }

    /// <summary>
    /// The same figures as <see cref="GetSummaryAsync"/>, period by period: grouped by Azerbaijani calendar day in SQL,
    /// then added up per day, month or year.
    /// </summary>
    public async Task<Result<FinanceMizanResponseDto>> GetMizanAsync(string companyId, string? storeId, string? period, CancellationToken cancellationToken)
    {
        period = NormalizePeriod(period);

        if (period is null)
            return Result<FinanceMizanResponseDto>.Failure(ErrorType.Validation, InvalidPeriod);

        storeId = string.IsNullOrWhiteSpace(storeId) ? null : storeId.Trim();

        if (storeId is not null && !await db.Stores.AnyAsync(s => s.Id == storeId && s.CompanyId == companyId, cancellationToken))
            return Result<FinanceMizanResponseDto>.Failure(ErrorType.NotFound, StoreNotFound);

        var today = Today;
        var current = SlotStart(today, period);
        var firstSlot = period switch
        {
            Daily => current.AddDays(1 - MizanDays),
            Yearly => current.AddYears(1 - MizanYears),
            _ => current.AddMonths(1 - MizanMonths)
        };

        var from = ToUtc(firstSlot);
        var to = ToUtc(NextSlot(current, period));

        var sales = db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.Status == SaleStatus.Approved && s.DocumentDate >= from && s.DocumentDate < to);

        var expenses = db.Expenses
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.ExpenseDate >= from && e.ExpenseDate < to);

        if (storeId is not null)
        {
            sales = sales.Where(s => s.StoreId == storeId);
            expenses = expenses.Where(e => e.StoreId == storeId);
        }

        // Grouped by local calendar day: UTC + 4 hours, then the date part.
        var revenue = await sales
            .GroupBy(s => new { Day = s.DocumentDate.AddHours(4).Date, s.Channel, IsWholesale = s.CustomerId != null })
            .Select(g => new { g.Key.Day, g.Key.Channel, g.Key.IsWholesale, Total = g.Sum(s => s.TotalAmount) })
            .ToListAsync(cancellationToken);

        var cogs = await (
                from item in db.SaleItems
                join sale in sales on item.SaleId equals sale.Id
                group new { item.Quantity, item.Product!.PurchasePrice } by sale.DocumentDate.AddHours(4).Date into g
                select new { Day = g.Key, Total = g.Sum(x => x.Quantity * x.PurchasePrice) })
            .ToListAsync(cancellationToken);

        var operating = await expenses
            .GroupBy(e => e.ExpenseDate.AddHours(4).Date)
            .Select(g => new { Day = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);

        var employees = await db.Employees
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && (storeId == null || e.StoreId == storeId))
            .Select(e => new { e.Salary, e.HiredAt, e.TerminatedAt })
            .ToListAsync(cancellationToken);

        var buckets = new SortedDictionary<DateOnly, Bucket>();
        Bucket For(DateTime day)
        {
            var slot = SlotStart(DateOnly.FromDateTime(day), period);
            return buckets.TryGetValue(slot, out var b) ? b : buckets[slot] = new Bucket();
        }

        foreach (var row in revenue)
        {
            var bucket = For(row.Day);

            if (row.Channel == SaleChannel.Online)
                bucket.Online += row.Total;
            else if (row.IsWholesale)
                bucket.Wholesale += row.Total;
            else
                bucket.Pos += row.Total;
        }

        foreach (var row in cogs)
            For(row.Day).Cogs += row.Total;

        foreach (var row in operating)
            For(row.Day).Operating += row.Total;

        // The current period always shows; monthly and yearly also every period someone was paid in. Daily shows
        // only days with activity, so the table is not 60 rows of salary alone.
        For(current.ToDateTime(TimeOnly.MinValue));

        if (period != Daily && employees.Count > 0)
        {
            var firstHire = SlotStart(employees.Min(e => e.HiredAt), period);

            for (var slot = firstHire > firstSlot ? firstHire : firstSlot; slot <= current; slot = NextSlot(slot, period))
                For(slot.ToDateTime(TimeOnly.MinValue));
        }

        foreach (var (slot, bucket) in buckets)
        {
            foreach (var employee in employees)
                bucket.Payroll += PayrollFor(employee.Salary, employee.HiredAt, employee.TerminatedAt, period, slot, today);
        }

        var rows = buckets
            .Reverse()
            .Select(pair => new FinanceMizanRowDto(
                SlotKey(pair.Key, period),
                ToUtc(pair.Key),
                ToUtc(NextSlot(pair.Key, period)),
                pair.Value.ToDto()))
            .ToList();

        return new FinanceMizanResponseDto(period, storeId, rows);
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow + BakuOffset);

    private static string? NormalizePeriod(string? period)
    {
        var value = string.IsNullOrWhiteSpace(period) ? Monthly : period.Trim().ToLowerInvariant();
        return value is Daily or Monthly or Yearly ? value : null;
    }

    private static DateOnly SlotStart(DateOnly day, string period) => period switch
    {
        Daily => day,
        Yearly => new DateOnly(day.Year, 1, 1),
        _ => new DateOnly(day.Year, day.Month, 1)
    };

    private static DateOnly NextSlot(DateOnly slot, string period) => period switch
    {
        Daily => slot.AddDays(1),
        Yearly => slot.AddYears(1),
        _ => slot.AddMonths(1)
    };

    private static string SlotKey(DateOnly slot, string period) => period switch
    {
        Daily => slot.ToString("yyyy-MM-dd"),
        Yearly => slot.ToString("yyyy"),
        _ => slot.ToString("yyyy-MM")
    };

    // Document dates are stored in UTC: Baku midnight is 20:00 UTC the day before.
    private static DateTime ToUtc(DateOnly day) => day.ToDateTime(TimeOnly.MinValue) - BakuOffset;

    /// <summary>
    /// Salary cost in the period starting on <paramref name="firstDay"/>. A month counts in full when the employee works
    /// at its end (hired by then, not terminated before), as on the HR page; a year adds up its months (so far, for the
    /// current year); a day is the month's salary divided by the month's days.
    /// </summary>
    private static decimal PayrollFor(decimal salary, DateOnly hiredAt, DateOnly? terminatedAt, string period, DateOnly firstDay, DateOnly today)
    {
        bool EmployedAt(DateOnly day) => hiredAt <= day && (terminatedAt is null || terminatedAt > day);

        if (period == Daily)
            return EmployedAt(firstDay) ? Math.Round(salary / DateTime.DaysInMonth(firstDay.Year, firstDay.Month), 2) : 0;

        var months = period == Yearly ? (firstDay.Year == today.Year ? today.Month : 12) : 1;
        var total = 0m;

        for (var i = 0; i < months; i++)
        {
            var monthStart = firstDay.AddMonths(i);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            if (EmployedAt(monthEnd))
                total += salary;
        }

        return total;
    }

    private sealed class Bucket
    {
        public decimal Pos, Wholesale, Online, Cogs, Operating, Payroll;

        public Bucket Add(Bucket other)
        {
            Pos += other.Pos;
            Wholesale += other.Wholesale;
            Online += other.Online;
            Cogs += other.Cogs;
            Operating += other.Operating;
            Payroll += other.Payroll;
            return this;
        }

        public FinanceTotalsDto ToDto()
        {
            // quantity × price has 5 decimals in SQL; money goes out with 2.
            var cogs = Math.Round(Cogs, 2, MidpointRounding.AwayFromZero);
            var revenue = Pos + Wholesale + Online;
            var expenses = cogs + Operating + Payroll;
            var net = revenue - expenses;
            decimal? margin = revenue == 0 ? null : Math.Round(net / revenue * 100, 1, MidpointRounding.AwayFromZero);

            return new FinanceTotalsDto(Pos, Wholesale, Online, revenue, cogs, Operating, Payroll, expenses, net, margin);
        }
    }
}
