using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Sales;

public class SaleService(IApplicationDbContext db, IStockWriter stockWriter) : ISaleService
{
    private const string NumberPrefix = "STŞ";

    // Upper bounds of decimal(18,2) money and decimal(18,3) quantity columns.
    private const decimal MaxMoney = 9_999_999_999_999_999.99m;
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    public async Task<Result<ItemsResponseDto<SaleSummaryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Sales
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderByDescending(s => s.DocumentDate)
            .ThenByDescending(s => s.DocumentNumber.Length).ThenByDescending(s => s.DocumentNumber)
            .Select(s => new SaleSummaryDto(
                s.Id,
                s.DocumentNumber,
                s.DocumentDate,
                s.Status,
                s.TotalAmount,
                s.StoreId,
                new CodeNameDto(s.Store!.Id, s.Store.Code, s.Store.Name),
                s.WarehouseId,
                new CodeNameDto(s.Warehouse!.Id, s.Warehouse.Code, s.Warehouse.Name),
                s.CustomerId,
                s.CustomerId == null ? null : new CodeNameDto(s.Customer!.Id, s.Customer.Code, s.Customer.Name),
                s.Items.Count))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<SaleSummaryDto>(items);
    }

    public async Task<Result<SaleLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        var sale = await FindAsync(companyId, id, cancellationToken);

        return sale is null
            ? Result<SaleLookupResponseDto>.Failure(ErrorType.NotFound, "Satış sənədi tapılmadı.")
            : new SaleLookupResponseDto(sale);
    }

    public async Task<Result<SaleResponseDto>> CreateAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken)
    {
        var built = await BuildSaleAsync(companyId, request, cancellationToken);

        if (!built.IsSuccess)
            return Result<SaleResponseDto>.Failure(built.ErrorType, built.Error!);

        var sale = built.Value!;
        sale.Status = SaleStatus.Draft;

        await AddWithNumberAsync(companyId, sale, cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);
    }

    public async Task<Result<SaleResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken)
    {
        // Status change and stock decreases commit together or not at all; disposing without Commit rolls back.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Compare-and-set on Status: if two approvals race, the second blocks on the row lock, then matches
        // nothing, so stock is never decreased twice for the same document.
        var approved = await db.Sales
            .Where(s => s.Id == id && s.CompanyId == companyId && s.Status == SaleStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SaleStatus.Approved), cancellationToken);

        if (approved == 0)
        {
            var exists = await db.Sales.AnyAsync(s => s.Id == id && s.CompanyId == companyId, cancellationToken);

            return exists
                ? Result<SaleResponseDto>.Failure(ErrorType.Conflict, "Sənəd artıq təsdiqlənib.")
                : Result<SaleResponseDto>.Failure(ErrorType.NotFound, "Satış sənədi tapılmadı.");
        }

        var warehouseId = await db.Sales
            .Where(s => s.Id == id)
            .Select(s => s.WarehouseId)
            .SingleAsync(cancellationToken);

        var lines = await db.SaleItems
            .Where(i => i.SaleId == id)
            .Select(i => new { i.ProductId, i.Quantity })
            .ToListAsync(cancellationToken);

        var shortage = await DecreaseStockAsync(
            companyId, warehouseId, lines.Select(l => (l.ProductId, l.Quantity)), cancellationToken);

        if (shortage is not null)
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, shortage);

        await transaction.CommitAsync(cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, id, cancellationToken))!);
    }

    public async Task<Result<SaleResponseDto>> PosCheckoutAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken)
    {
        var built = await BuildSaleAsync(companyId, request, cancellationToken);

        if (!built.IsSuccess)
            return Result<SaleResponseDto>.Failure(built.ErrorType, built.Error!);

        var sale = built.Value!;
        sale.Status = SaleStatus.Approved;

        // Stock decrease and the approved document commit together; on a shortage nothing is saved at all.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var shortage = await DecreaseStockAsync(
            companyId, sale.WarehouseId, sale.Items.Select(i => (i.ProductId, i.Quantity)), cancellationToken);

        if (shortage is not null)
            return Result<SaleResponseDto>.Failure(ErrorType.Validation, shortage);

        await AddWithNumberAsync(companyId, sale, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return SaleResponseDto.From((await FindAsync(companyId, sale.Id, cancellationToken))!);
    }

    /// <summary>
    /// Validates the request against the database and builds an unsaved sale with its lines and totals.
    /// Shared by draft creation and POS checkout, which differ only in status and stock handling.
    /// </summary>
    private async Task<Result<Sale>> BuildSaleAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken)
    {
        var storeId = request.StoreId?.Trim();
        var requestedWarehouseId = string.IsNullOrWhiteSpace(request.WarehouseId) ? null : request.WarehouseId.Trim();
        var customerId = string.IsNullOrWhiteSpace(request.CustomerId) ? null : request.CustomerId.Trim();

        if (string.IsNullOrEmpty(storeId))
            return Result<Sale>.Failure(ErrorType.Validation, "Mağaza seçilməlidir.");

        if (request.Items is not { Count: > 0 } requestItems)
            return Result<Sale>.Failure(ErrorType.Validation, "Sənəddə ən azı bir məhsul olmalıdır.");

        // Validate line shapes before touching the database. Line numbers in messages are 1-based, as the user sees them.
        var lines = new List<(string ProductId, decimal Quantity, decimal? UnitPrice)>(requestItems.Count);

        for (var i = 0; i < requestItems.Count; i++)
        {
            var line = i + 1;
            var item = requestItems[i];
            var productId = item?.ProductId?.Trim();

            if (item is null || string.IsNullOrEmpty(productId))
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: məhsul seçilməlidir.");

            if (item.Quantity is not { } rawQuantity)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar tələb olunur.");

            // Rounded to the column scale first, so what is validated is exactly what gets stored.
            var quantity = Math.Round(rawQuantity, 3, MidpointRounding.AwayFromZero);
            var unitPrice = item.UnitPrice is { } rawUnitPrice ? Math.Round(rawUnitPrice, 2, MidpointRounding.AwayFromZero) : (decimal?)null;

            if (quantity <= 0)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar sıfırdan böyük olmalıdır.");

            if (quantity > MaxQuantity)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: miqdar həddindən artıq böyükdür.");

            if (unitPrice < 0)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: qiymət mənfi ola bilməz.");

            lines.Add((productId, quantity, unitPrice));
        }

        // Scoped to the caller's company, so another company's records look exactly like missing ones.
        var store = await db.Stores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == storeId && s.CompanyId == companyId, cancellationToken);

        if (store is null)
            return Result<Sale>.Failure(ErrorType.NotFound, "Mağaza tapılmadı.");

        if (!store.IsActive)
            return Result<Sale>.Failure(ErrorType.Validation, "Seçilmiş mağaza aktiv deyil.");

        var warehouseId = requestedWarehouseId ?? store.WarehouseId;
        var warehouse = await db.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == warehouseId && w.CompanyId == companyId, cancellationToken);

        if (warehouse is null)
            return Result<Sale>.Failure(ErrorType.NotFound, "Anbar tapılmadı.");

        if (!warehouse.IsActive)
            return Result<Sale>.Failure(ErrorType.Validation, "Seçilmiş anbar aktiv deyil.");

        if (customerId is not null)
        {
            var customer = await db.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.CompanyId == companyId, cancellationToken);

            if (customer is null)
                return Result<Sale>.Failure(ErrorType.NotFound, "Kontragent tapılmadı.");

            if (!customer.IsActive)
                return Result<Sale>.Failure(ErrorType.Validation, "Seçilmiş kontragent aktiv deyil.");
        }

        var productIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.CompanyId == companyId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IsActive, p.SalePrice })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var sale = new Sale
        {
            CompanyId = companyId,
            StoreId = store.Id,
            WarehouseId = warehouse.Id,
            CustomerId = customerId,
            DocumentDate = request.DocumentDate ?? DateTime.UtcNow
        };

        for (var i = 0; i < lines.Count; i++)
        {
            var line = i + 1;
            var (productId, quantity, requestedUnitPrice) = lines[i];

            if (!products.TryGetValue(productId, out var product))
                return Result<Sale>.Failure(ErrorType.NotFound, $"Sətir {line}: məhsul tapılmadı.");

            if (!product.IsActive)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: məhsul aktiv deyil.");

            var unitPrice = requestedUnitPrice ?? product.SalePrice;

            // Checked by division so the multiplication below can never overflow.
            if (unitPrice > 0 && quantity > MaxMoney / unitPrice)
                return Result<Sale>.Failure(ErrorType.Validation, $"Sətir {line}: məbləğ həddindən artıq böyükdür.");

            sale.Items.Add(new SaleItem
            {
                SaleId = sale.Id,
                LineNumber = line,
                ProductId = productId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalPrice = Math.Round(quantity * unitPrice, 2, MidpointRounding.AwayFromZero)
            });
        }

        // Each line fits on its own; with many lines the sum can still exceed the column.
        var totalAmount = sale.Items.Sum(i => i.TotalPrice);

        if (totalAmount > MaxMoney)
            return Result<Sale>.Failure(ErrorType.Validation, "Sənədin ümumi məbləği həddindən artıq böyükdür.");

        sale.TotalAmount = totalAmount;

        return sale;
    }

    private Task AddWithNumberAsync(string companyId, Sale sale, CancellationToken cancellationToken)
    {
        db.Sales.Add(sale);

        return SequentialCode.AssignAndSaveAsync(
            db,
            db.Sales.Where(s => s.CompanyId == companyId).Select(s => s.DocumentNumber),
            NumberPrefix,
            number => sale.DocumentNumber = number,
            cancellationToken);
    }

    /// <summary>
    /// Decreases stock for every product, all or nothing within the caller's transaction. Returns null on
    /// success, or the shortage message for the first product that is not covered (the caller must then
    /// not commit, which undoes any decreases already made).
    /// </summary>
    private async Task<string?> DecreaseStockAsync(
        string companyId,
        string warehouseId,
        IEnumerable<(string ProductId, decimal Quantity)> lines,
        CancellationToken cancellationToken)
    {
        // One decrease per product (a product may span several lines), in a fixed ordinal order shared with
        // purchase approval so concurrent documents lock stock rows in the same sequence and do not deadlock.
        var required = lines
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .OrderBy(r => r.ProductId, StringComparer.Ordinal);

        foreach (var (productId, quantity) in required)
        {
            if (await stockWriter.TryDecreaseAsync(companyId, warehouseId, productId, quantity, cancellationToken))
                continue;

            var available = await db.Stocks
                .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouseId && s.ProductId == productId)
                .Select(s => (decimal?)s.Quantity)
                .FirstOrDefaultAsync(cancellationToken) ?? 0m;

            var productName = await db.Products
                .Where(p => p.Id == productId)
                .Select(p => p.Name)
                .FirstAsync(cancellationToken);

            return $"Anbarda satış üçün kifayət qədər məhsul yoxdur: {productName} " +
                   $"(Qalıq: {FormatQuantity(available)}, Tələb olunan: {FormatQuantity(quantity)})";
        }

        return null;
    }

    private static string FormatQuantity(decimal quantity) => quantity.ToString("0.###", CultureInfo.InvariantCulture);

    private Task<SaleDto?> FindAsync(string companyId, string id, CancellationToken cancellationToken) =>
        db.Sales
            .AsNoTracking()
            .Where(s => s.Id == id && s.CompanyId == companyId)
            .Select(SaleDto.Projection)
            .FirstOrDefaultAsync(cancellationToken);
}
