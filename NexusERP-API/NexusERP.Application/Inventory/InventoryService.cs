using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;

namespace NexusERP.Application.Inventory;

public class InventoryService(IApplicationDbContext db) : IInventoryService
{
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
}
