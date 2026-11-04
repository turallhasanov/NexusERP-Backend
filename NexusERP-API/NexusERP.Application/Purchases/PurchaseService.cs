using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Purchases;

public class PurchaseService(
    IApplicationDbContext db, IStockWriter stockWriter, IAuditLogger audit, IStorefrontStockSync storefrontSync) : IPurchaseService
{
    private const string NumberPrefix = "ALS";

    // Upper bounds of decimal(18,2) money and decimal(18,3) quantity columns.
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    public async Task<Result<ItemsResponseDto<PurchaseSummaryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Purchases
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId)
            .OrderByDescending(p => p.DocumentDate)
            .ThenByDescending(p => p.DocumentNumber.Length).ThenByDescending(p => p.DocumentNumber)
            .Select(p => new PurchaseSummaryDto(
                p.Id,
                p.DocumentNumber,
                p.DocumentDate,
                p.Status,
                p.TotalAmount,
                p.WarehouseId,
                new CodeNameDto(p.Warehouse!.Id, p.Warehouse.Code, p.Warehouse.Name),
                p.CustomerId,
                new CodeNameDto(p.Customer!.Id, p.Customer.Code, p.Customer.Name),
                p.Items.Count))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<PurchaseSummaryDto>(items);
    }

    public async Task<Result<PurchaseLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var purchase = await FindAsync(companyId, id, cancellationToken);

        return purchase is null
            ? Result<PurchaseLookupResponseDto>.Failure(ErrorType.NotFound, "Alış sənədi tapılmadı.")
            : new PurchaseLookupResponseDto(purchase);
    }

    public async Task<Result<PurchaseResponseDto>> CreateAsync(string companyId, CreatePurchaseRequestDto request, CancellationToken cancellationToken)
    {
        var warehouseId = request.WarehouseId?.Trim();
        var customerId = request.CustomerId?.Trim();

        if (string.IsNullOrEmpty(warehouseId))
            return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, "Anbar seçilməlidir.");

        if (string.IsNullOrEmpty(customerId))
            return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, "Kontragent seçilməlidir.");

        if (request.Items is not { Count: > 0 } requestItems)
            return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, "Sənəddə ən azı bir məhsul olmalıdır.");

        // Validate every line before touching the database. Line numbers in messages are 1-based, as the user sees them.
        var lines = new List<(string ProductId, decimal Quantity, decimal UnitPrice)>(requestItems.Count);

        for (var i = 0; i < requestItems.Count; i++)
        {
            var line = i + 1;
            var item = requestItems[i];
            var productId = item?.ProductId?.Trim();

            if (item is null || string.IsNullOrEmpty(productId))
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: məhsul seçilməlidir.");

            if (item.Quantity is not { } rawQuantity)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar tələb olunur.");

            if (item.UnitPrice is not { } rawUnitPrice)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: qiymət tələb olunur.");

            // Rounded to the column scale first, so what is validated is exactly what gets stored.
            var quantity = Math.Round(rawQuantity, 3, MidpointRounding.AwayFromZero);
            var unitPrice = Math.Round(rawUnitPrice, 2, MidpointRounding.AwayFromZero);

            if (quantity <= 0)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar sıfırdan böyük olmalıdır.");

            if (quantity > MaxQuantity)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar həddindən artıq böyükdür.");

            if (unitPrice < 0)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: qiymət mənfi ola bilməz.");

            // Checked by division so the multiplication below can never overflow.
            if (unitPrice > 0 && quantity > MaxMoney / unitPrice)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {line}: məbləğ həddindən artıq böyükdür.");

            lines.Add((productId, quantity, unitPrice));
        }

        // Scoped to the caller's company, so another company's records look exactly like missing ones.
        var warehouse = await db.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.CompanyId == companyId, cancellationToken);

        if (warehouse is null)
            return Result<PurchaseResponseDto>.Failure(ErrorType.NotFound, "Anbar tapılmadı.");

        if (!warehouse.IsActive)
            return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, "Seçilmiş anbar aktiv deyil.");

        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken);

        if (customer is null)
            return Result<PurchaseResponseDto>.Failure(ErrorType.NotFound, "Kontragent tapılmadı.");

        if (!customer.IsActive)
            return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, "Seçilmiş kontragent aktiv deyil.");

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var productIsActive = await db.Products
            .Where(p => p.CompanyId == companyId && productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.IsActive, cancellationToken);

        for (var i = 0; i < lines.Count; i++)
        {
            if (!productIsActive.TryGetValue(lines[i].ProductId, out var isActive))
                return Result<PurchaseResponseDto>.Failure(ErrorType.NotFound, $"Sətir {i + 1}: məhsul tapılmadı.");

            if (!isActive)
                return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, $"Sətir {i + 1}: məhsul aktiv deyil.");
        }

        var purchase = new Purchase
        {
            CompanyId = companyId,
            WarehouseId = warehouse.Id,
            CustomerId = customer.Id,
            DocumentDate = request.DocumentDate ?? DateTime.UtcNow,
            Status = PurchaseStatus.Draft
        };

        for (var i = 0; i < lines.Count; i++)
        {
            var (productId, quantity, unitPrice) = lines[i];

            purchase.Items.Add(new PurchaseItem
            {
                PurchaseId = purchase.Id,
                LineNumber = i + 1,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero)
            });
        }

        // Each line fits on its own; with many lines the sum can still exceed the column.
        var totalAmount = purchase.Items.Sum(i => i.TotalPrice);

        if (totalAmount > MaxMoney)
            return Result<PurchaseResponseDto>.Failure(ErrorType.Validation, "Sənədin ümumi məbləği həddindən artıq böyükdür.");

        purchase.TotalAmount = totalAmount;

        db.Purchases.Add(purchase);
        await SequentialCode.AssignAndSaveAsync(
            db,
            db.Purchases.Where(p => p.CompanyId == companyId).Select(p => p.DocumentNumber),
            NumberPrefix,
            number => purchase.DocumentNumber = number,
            cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Finance, AuditAction.Create,
            $"'{purchase.DocumentNumber}' alış qaiməsi yaradıldı ({purchase.TotalAmount:0.00} ₼)", cancellationToken);

        return PurchaseResponseDto.From((await FindAsync(companyId, purchase.Id, cancellationToken))!);
    }

    public async Task<Result<PurchaseResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        // Status change and stock increases commit together or not at all; disposing without Commit rolls back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Compare-and-set on Status: if two approvals race, the second blocks on the row lock, then matches
        // nothing, so stock is never increased twice for the same document.
        var approved = await db.Purchases
            .Where(p => p.Id == id && p.CompanyId == companyId && p.Status == PurchaseStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PurchaseStatus.Approved), cancellationToken);

        if (approved == 0)
        {
            var exists = await db.Purchases.AnyAsync(p => p.Id == id && p.CompanyId == companyId, cancellationToken);

            return exists
                ? Result<PurchaseResponseDto>.Failure(ErrorType.Conflict, "Sənəd artıq təsdiqlənib.")
                : Result<PurchaseResponseDto>.Failure(ErrorType.NotFound, "Alış sənədi tapılmadı.");
        }

        var warehouseId = await db.Purchases
            .Where(p => p.Id == id)
            .Select(p => p.WarehouseId)
            .SingleAsync(cancellationToken);

        // One increment per product. A fixed order means concurrent approvals lock stock rows in the
        // same sequence, which avoids deadlocks between them. Sorted in memory (ordinal), not in SQL,
        // so the order matches sale approval's exactly regardless of database collation.
        var increments = await db.PurchaseItems
            .Where(i => i.PurchaseId == id)
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);

        foreach (var increment in increments.OrderBy(i => i.ProductId, StringComparer.Ordinal))
            await stockWriter.IncreaseAsync(companyId, warehouseId, increment.ProductId, increment.Quantity, cancellationToken);

        // The product's purchase price becomes what was just paid for it: the unit price of its last line in
        // this document. Same product order as the stock updates above.
        var lines = await db.PurchaseItems
            .Where(i => i.PurchaseId == id)
            .Select(i => new { i.ProductId, i.LineNumber, i.UnitPrice })
            .ToListAsync(cancellationToken);

        var lastPrices = lines
            .GroupBy(l => l.ProductId)
            .Select(g => g.MaxBy(l => l.LineNumber)!)
            .OrderBy(l => l.ProductId, StringComparer.Ordinal);

        foreach (var line in lastPrices)
        {
            await db.Products
                .Where(p => p.Id == line.ProductId && p.CompanyId == companyId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PurchasePrice, line.UnitPrice), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        // After the commit: a Khazar failure is logged inside and cannot undo the approval.
        await storefrontSync.SyncAsync(companyId, increments.Select(i => i.ProductId).ToList(), cancellationToken);

        var approvedPurchase = (await FindAsync(companyId, id, cancellationToken))!;
        await audit.LogAsync(companyId, AuditModule.Finance, AuditAction.Update,
            $"'{approvedPurchase.DocumentNumber}' alış qaiməsi təsdiqləndi ({approvedPurchase.TotalAmount:0.00} ₼)", cancellationToken);

        return PurchaseResponseDto.From(approvedPurchase);
    }

    public async Task<Result<PurchaseResponseDto>> CancelAsync(string companyId, string id, bool force, CancellationToken cancellationToken)
    {
        var purchase = await db.Purchases
            .AsNoTracking()
            .Where(p => p.Id == id && p.CompanyId == companyId)
            .Select(p => new { p.Id, p.DocumentNumber, p.WarehouseId, p.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (purchase is null)
            return Result<PurchaseResponseDto>.Failure(ErrorType.NotFound, "Alış sənədi tapılmadı.");

        // A retry after a lost response must not take the stock out twice.
        if (purchase.Status == PurchaseStatus.Cancelled)
            return PurchaseResponseDto.From((await FindAsync(companyId, id, cancellationToken))!);

        if (await db.Transactions.AnyAsync(t => t.PurchaseId == id, cancellationToken))
            return Result<PurchaseResponseDto>.Failure(
                ErrorType.Conflict,
                $"'{purchase.DocumentNumber}' alışına kassa ödənişi bağlıdır, ləğv etmək mümkün deyil.");

        // Status change and stock decreases commit together or not at all; disposing without Commit rolls back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Compare-and-set on the status that was read: if two cancellations race, only one matches a row,
        // so stock is never taken out twice.
        var changed = await db.Purchases
            .Where(p => p.Id == id && p.Status == purchase.Status)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, PurchaseStatus.Cancelled), cancellationToken);

        if (changed == 0)
            return Result<PurchaseResponseDto>.Failure(ErrorType.Conflict, "Sənəd eyni anda dəyişdirildi. Yenidən cəhd edin.");

        // Units the warehouse no longer held when a forced cancellation ran (sold, or cleared by a stock reset).
        var missing = new List<string>();
        var touchedProductIds = new List<string>();

        // A draft never added stock, so only an approved purchase has anything to take back.
        if (purchase.Status == PurchaseStatus.Approved)
        {
            var lines = await db.PurchaseItems
                .Where(i => i.PurchaseId == id)
                .GroupBy(i => i.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
                .ToListAsync(cancellationToken);

            touchedProductIds.AddRange(lines.Select(l => l.ProductId));

            // Same fixed ordinal order as approval, so concurrent documents lock stock rows in one sequence.
            foreach (var line in lines.OrderBy(l => l.ProductId, StringComparer.Ordinal))
            {
                if (await stockWriter.TryDecreaseAsync(companyId, purchase.WarehouseId, line.ProductId, line.Quantity, cancellationToken))
                    continue;

                var available = await db.Stocks
                    .Where(s => s.CompanyId == companyId && s.WarehouseId == purchase.WarehouseId && s.ProductId == line.ProductId)
                    .Select(s => (decimal?)s.Quantity)
                    .FirstOrDefaultAsync(cancellationToken) ?? 0m;

                var productName = await db.Products
                    .Where(p => p.Id == line.ProductId)
                    .Select(p => p.Name)
                    .FirstAsync(cancellationToken);

                if (!force)
                    return Result<PurchaseResponseDto>.Failure(
                        ErrorType.StockShortage,
                        $"'{purchase.DocumentNumber}' alışını ləğv etmək mümkün deyil: {productName} malının bir hissəsi artıq anbarda yoxdur " +
                        $"(Qalıq: {FormatQuantity(available)}, Geri götürülməli: {FormatQuantity(line.Quantity)}).");

                // Forced: take what is there, so the balance ends at zero and never goes negative.
                if (available > 0
                    && !await stockWriter.TryDecreaseAsync(companyId, purchase.WarehouseId, line.ProductId, available, cancellationToken))
                    return Result<PurchaseResponseDto>.Failure(ErrorType.Conflict, "Anbar qalığı eyni anda dəyişdirildi. Yenidən cəhd edin.");

                missing.Add($"{productName}: {FormatQuantity(line.Quantity - Math.Max(available, 0m))} ədəd çatmırdı");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        await storefrontSync.SyncAsync(companyId, touchedProductIds, cancellationToken);

        var cancelled = (await FindAsync(companyId, id, cancellationToken))!;
        await audit.LogAsync(companyId, AuditModule.Finance, AuditAction.Update,
            missing.Count == 0
                ? $"'{cancelled.DocumentNumber}' alış qaiməsi ləğv edildi, məhsullar anbardan geri götürüldü ({cancelled.TotalAmount:0.00} ₼)"
                : $"'{cancelled.DocumentNumber}' alış qaiməsi məcburi ləğv edildi ({cancelled.TotalAmount:0.00} ₼); anbarda olmayan miqdar keçildi, qalıq sıfırlandı ({string.Join("; ", missing)})",
            cancellationToken);

        return PurchaseResponseDto.From(cancelled);
    }

    private static string FormatQuantity(decimal quantity) =>
        quantity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private Task<PurchaseDto?> FindAsync(string companyId, string id, CancellationToken cancellationToken) =>
        db.Purchases
            .AsNoTracking()
            .Where(p => p.Id == id && p.CompanyId == companyId)
            .Select(PurchaseDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);
}
