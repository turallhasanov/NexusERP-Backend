using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Stores;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateStoreRequestDto(string? Name, string? WarehouseId, string? Address);

public sealed record UpdateStoreRequestDto(string? Name, string? WarehouseId, string? Address);

/// <summary>The warehouse a store is supplied from, embedded in store responses.</summary>
public sealed record StoreWarehouseDto(string Id, string Code, string Name)
{
    public static StoreWarehouseDto From(Warehouse warehouse) => new(warehouse.Id, warehouse.Code, warehouse.Name);
}

public sealed record StoreDto(
    string Id,
    string Code,
    string Name,
    string? Address,
    bool IsActive,
    string WarehouseId,
    StoreWarehouseDto Warehouse);

/// <summary>POST /api/stores and PUT /api/stores/{id} → { "ok": true, "id", "code", "name", "address", "isActive", "warehouseId", "warehouse": { ... } }</summary>
public sealed record StoreResponseDto(
    string Id,
    string Code,
    string Name,
    string? Address,
    bool IsActive,
    string WarehouseId,
    StoreWarehouseDto Warehouse) : SuccessResponse
{
    public static StoreResponseDto From(Store store, Warehouse warehouse) =>
        new(store.Id, store.Code, store.Name, store.Address, store.IsActive, store.WarehouseId, StoreWarehouseDto.From(warehouse));
}
