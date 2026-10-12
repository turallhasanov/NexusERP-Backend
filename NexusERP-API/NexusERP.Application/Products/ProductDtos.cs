using System.Linq.Expressions;
using NexusERP.Application.Categories;
using NexusERP.Application.Common;
using NexusERP.Application.Units;
using NexusERP.Domain.Entities;

namespace NexusERP.Application.Products;

/// <summary>
/// Fields are nullable so missing values reach our own validation instead of the framework's.
/// An empty <c>barcode</c> means "generate one".
/// </summary>
public sealed record CreateProductRequestDto(
    string? CategoryId,
    string? UnitId,
    string? Name,
    string? Barcode,
    decimal? PurchasePrice,
    decimal? SalePrice);

public sealed record ProductDto(
    string Id,
    string Code,
    string Name,
    string Barcode,
    decimal PurchasePrice,
    decimal SalePrice,
    bool IsActive,
    string CategoryId,
    CategoryDto Category,
    string UnitId,
    UnitDto Unit)
{
    /// <summary>Server-side projection shared by the list and barcode lookups.</summary>
    public static readonly Expression<Func<Product, ProductDto>> Projection = p => new ProductDto(
        p.Id,
        p.Code,
        p.Name,
        p.Barcode,
        p.PurchasePrice,
        p.SalePrice,
        p.IsActive,
        p.CategoryId,
        new CategoryDto(p.Category!.Id, p.Category.Code, p.Category.Name),
        p.UnitId,
        new UnitDto(p.Unit!.Id, p.Unit.Name, p.Unit.ShortName));
}

/// <summary>GET /api/products/barcode/{barcode} → { "ok": true, "product": { ... } }</summary>
public sealed record ProductLookupResponseDto(ProductDto Product) : SuccessResponse;

/// <summary>POST /api/products → { "ok": true, "id", "code", ..., "category": { ... }, "unit": { ... } }</summary>
public sealed record ProductResponseDto(
    string Id,
    string Code,
    string Name,
    string Barcode,
    decimal PurchasePrice,
    decimal SalePrice,
    bool IsActive,
    string CategoryId,
    CategoryDto Category,
    string UnitId,
    UnitDto Unit) : SuccessResponse
{
    public static ProductResponseDto From(Product product, Category category, Unit unit) =>
        new(
            product.Id,
            product.Code,
            product.Name,
            product.Barcode,
            product.PurchasePrice,
            product.SalePrice,
            product.IsActive,
            product.CategoryId,
            new CategoryDto(category.Id, category.Code, category.Name),
            product.UnitId,
            new UnitDto(unit.Id, unit.Name, unit.ShortName));
}
