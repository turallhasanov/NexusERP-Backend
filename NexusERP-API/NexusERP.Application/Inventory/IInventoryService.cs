using NexusERP.Application.Common;

namespace NexusERP.Application.Inventory;

public interface IInventoryService
{
    Task<Result<ItemsResponseDto<StockDto>>> GetStocksAsync(string companyId, CancellationToken cancellationToken);

    /// <summary>Adds stock to one warehouse (manual receipt) and sends the new total to the Khazar store.</summary>
    Task<Result<ReceiveStockResponseDto>> ReceiveStockAsync(string companyId, ReceiveStockRequestDto request, CancellationToken cancellationToken);
}
