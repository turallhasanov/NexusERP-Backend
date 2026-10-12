using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Warehouses;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateWarehouseRequestDto(string? Name, string? Address);

public sealed record WarehouseDto(string Id, string Code, string Name, string? Address, bool IsActive);

/// <summary>POST /api/warehouses → { "ok": true, "id", "code", "name", "address", "isActive" }</summary>
public sealed record WarehouseResponseDto(string Id, string Code, string Name, string? Address, bool IsActive) : SuccessResponse
{
    public static WarehouseResponseDto From(Warehouse warehouse) =>
        new(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
}
