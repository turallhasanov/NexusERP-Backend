using NexusERP.Application.Common;

namespace NexusERP.Application.Inventory;

/// <summary>Row in GET /api/inventory/stocks: balance of one product in one warehouse.</summary>
public sealed record StockDto(
    string Id,
    string WarehouseId,
    CodeNameDto Warehouse,
    string ProductId,
    ProductRefDto Product,
    decimal Quantity);
