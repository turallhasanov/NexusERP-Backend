using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Purchases;

public class PurchaseService(IApplicationDbContext db, IStockWriter stockWriter) : IPurchaseService
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

        return PurchaseResponseDto.From((await FindAsync(companyId, id, cancellationToken))!);
    }

    private Task<PurchaseDto?> FindAsync(string companyId, string id, CancellationToken cancellationToken) =>
        db.Purchases
            .AsNoTracking()
            .Where(p => p.Id == id && p.CompanyId == companyId)
            .Select(PurchaseDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);
}
