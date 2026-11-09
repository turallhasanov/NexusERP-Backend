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

/// <summary>Body of POST /api/inventory/receive.</summary>
public sealed record ReceiveStockRequestDto(string? WarehouseId, string? ProductId, decimal Quantity);

/// <summary>POST /api/inventory/receive → the warehouse balance and the product's total after the receipt.</summary>
public sealed record ReceiveStockResponseDto(string WarehouseId, string ProductId, decimal WarehouseQuantity, decimal TotalQuantity)
    : SuccessResponse;
