using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Units;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateUnitRequestDto(string? Name, string? ShortName);

public sealed record UnitDto(string Id, string Name, string ShortName);

/// <summary>POST /api/units → { "ok": true, "id", "name", "shortName" }</summary>
public sealed record UnitResponseDto(string Id, string Name, string ShortName) : SuccessResponse
{
    public static UnitResponseDto From(Unit unit) => new(unit.Id, unit.Name, unit.ShortName);
}
