using NexusERP.Application.Common;

namespace NexusERP.Application.Inventory;

public interface IInventoryService
{
    Task<Result<ItemsResponseDto<StockDto>>> GetStocksAsync(string companyId, CancellationToken cancellationToken);
}
