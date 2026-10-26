using NexusERP.Application.Common;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Categories;

/// <summary>Fields are nullable so missing values reach our own validation instead of the framework's.</summary>
public sealed record CreateCategoryRequestDto(string? Name);

public sealed record UpdateCategoryRequestDto(string? Name);

public sealed record CategoryDto(string Id, string Code, string Name);

/// <summary>POST /api/categories and PUT /api/categories/{id} → { "ok": true, "id", "code", "name" }</summary>
public sealed record CategoryResponseDto(string Id, string Code, string Name) : SuccessResponse
{
    public static CategoryResponseDto From(Category category) => new(category.Id, category.Code, category.Name);
}
