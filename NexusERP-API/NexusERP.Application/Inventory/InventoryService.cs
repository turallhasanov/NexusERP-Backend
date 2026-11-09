using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Application.Integration;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Inventory;

public class InventoryService(
    IApplicationDbContext db, IStockWriter stockWriter, IAuditLogger audit, IStorefrontStockSync storefrontSync) : IInventoryService
{
    // Upper bound of the decimal(18,3) quantity column.
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    public async Task<Result<ItemsResponseDto<StockDto>>> GetStocksAsync(string companyId, CancellationToken cancellationToken)
    {
        var items = await db.Stocks
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.Warehouse!.Name).ThenBy(s => s.Product!.Name)
            .Select(s => new StockDto(
                s.Id,
                s.WarehouseId,
                new CodeNameDto(s.Warehouse!.Id, s.Warehouse.Code, s.Warehouse.Name),
                s.ProductId,
                new ProductRefDto(s.Product!.Id, s.Product.Code, s.Product.Name, s.Product.Barcode, s.Product.Unit!.ShortName),
                s.Quantity))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<StockDto>(items);
    }

    public async Task<Result<ReceiveStockResponseDto>> ReceiveStockAsync(
        string companyId, ReceiveStockRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.WarehouseId) || string.IsNullOrWhiteSpace(request.ProductId))
            return Result<ReceiveStockResponseDto>.Failure(ErrorType.Validation, "Məhsul və anbar tələb olunur.");

        var quantity = Math.Round(request.Quantity, 3);

        if (quantity <= 0 || quantity > MaxQuantity)
            return Result<ReceiveStockResponseDto>.Failure(ErrorType.Validation, "Say 0-dan böyük olmalıdır.");

        // Scoped to the caller's company, so another company's records look exactly like missing ones.
        var warehouse = await db.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.CompanyId == companyId, cancellationToken);

        if (warehouse is null)
            return Result<ReceiveStockResponseDto>.Failure(ErrorType.NotFound, "Anbar tapılmadı.");

        if (!warehouse.IsActive)
            return Result<ReceiveStockResponseDto>.Failure(ErrorType.Validation, "Seçilmiş anbar aktiv deyil.");

        var product = await db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.CompanyId == companyId, cancellationToken);

        if (product is null)
            return Result<ReceiveStockResponseDto>.Failure(ErrorType.NotFound, "Məhsul tapılmadı.");

        if (!product.IsActive)
            return Result<ReceiveStockResponseDto>.Failure(ErrorType.Validation, "Məhsul aktiv deyil.");

        await stockWriter.IncreaseAsync(companyId, warehouse.Id, product.Id, quantity, cancellationToken);

        await audit.LogAsync(companyId, AuditModule.Inventory, AuditAction.Update,
            $"'{product.Code} · {product.Name}' məhsuluna '{warehouse.Name}' anbarında {quantity:0.###} stok əlavə edildi",
            cancellationToken);

        // After the write: a Khazar failure is logged inside and cannot undo the receipt.
        await storefrontSync.SyncAsync(companyId, [product.Id], cancellationToken);

        var warehouseQuantity = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.WarehouseId == warehouse.Id && s.ProductId == product.Id)
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0m;
        var total = await db.Stocks
            .Where(s => s.CompanyId == companyId && s.ProductId == product.Id)
            .SumAsync(s => (decimal?)s.Quantity, cancellationToken) ?? 0m;

        return new ReceiveStockResponseDto(warehouse.Id, product.Id, warehouseQuantity, total);
    }
}
