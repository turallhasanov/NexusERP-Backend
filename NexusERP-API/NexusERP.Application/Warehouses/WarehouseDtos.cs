using System.Linq.Expressions;
using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Warehouses;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateWarehouseRequestDto(string? Name, string? Address, string? ResponsiblePerson, decimal? Capacity);

public sealed record UpdateWarehouseRequestDto(string? Name, string? Address, string? ResponsiblePerson, decimal? Capacity);

/// <summary>
/// Row in GET /api/warehouses. <c>productCount</c> counts products with a non-zero balance here;
/// <c>totalQuantity</c> sums those balances across units.
/// </summary>
public sealed record WarehouseDto(
    string Id,
    string Code,
    string Name,
    string? Address,
    string? ResponsiblePerson,
    decimal? Capacity,
    bool IsActive,
    int ProductCount,
    decimal TotalQuantity)
{
    public static Expression<Func<Warehouse, WarehouseDto>> Projection(IQueryable<Stock> stocks) => w => new WarehouseDto(
        w.Id,
        w.Code,
        w.Name,
        w.Address,
        w.ResponsiblePerson,
        w.Capacity,
        w.IsActive,
        stocks.Count(s => s.WarehouseId == w.Id && s.Quantity != 0),
        stocks.Where(s => s.WarehouseId == w.Id).Sum(s => (decimal?)s.Quantity) ?? 0);
}

/// <summary>POST /api/warehouses and PUT /api/warehouses/{id} → { "ok": true, "id", "code", "name", ... }</summary>
public sealed record WarehouseResponseDto(
    string Id,
    string Code,
    string Name,
    string? Address,
    string? ResponsiblePerson,
    decimal? Capacity,
    bool IsActive,
    int ProductCount,
    decimal TotalQuantity) : SuccessResponse
{
    public static WarehouseResponseDto From(WarehouseDto w) =>
        new(w.Id, w.Code, w.Name, w.Address, w.ResponsiblePerson, w.Capacity, w.IsActive, w.ProductCount, w.TotalQuantity);
}

/// <summary>Balance of one product in the warehouse.</summary>
public sealed record WarehouseStockDto(string ProductId, ProductRefDto Product, decimal Quantity);

public enum WarehouseMovementType
{
    /// <summary>Approved purchase: goods came in (mədaxil).</summary>
    In = 1,

    /// <summary>Approved sale: goods went out (məxaric).</summary>
    Out = 2
}

/// <summary>An approved purchase or sale that changed the warehouse's stock.</summary>
public sealed record WarehouseMovementDto(
    string DocumentId,
    string DocumentNumber,
    DateTime DocumentDate,
    WarehouseMovementType Type,
    string? Counterparty,
    decimal Quantity,
    decimal Amount);

/// <summary>GET /api/warehouses/{id} → { "ok": true, "warehouse", "stocks": [ ... ], "movements": [ ... ] }</summary>
public sealed record WarehouseDetailsResponseDto(
    WarehouseDto Warehouse,
    IReadOnlyList<WarehouseStockDto> Stocks,
    IReadOnlyList<WarehouseMovementDto> Movements) : SuccessResponse;
