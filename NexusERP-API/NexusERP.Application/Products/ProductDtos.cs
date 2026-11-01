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
    decimal? SalePrice,
    bool ShowOnWebsite = false);

/// <summary>PUT /api/products/{id}. The barcode cannot change: it is the product's SKU in the Khazar store.</summary>
public sealed record UpdateProductRequestDto(
    string? CategoryId,
    string? UnitId,
    string? Name,
    decimal? PurchasePrice,
    decimal? SalePrice,
    bool ShowOnWebsite);

/// <summary>
/// Whether the Khazar store got the product: "synced" (added or updated there), "hidden" (taken off the store),
/// "notShown" (not meant for the store) or "failed" (<c>message</c> says why; the product itself is saved).
/// </summary>
public sealed record StorefrontSyncDto(string Status, string? Message = null)
{
    public static readonly StorefrontSyncDto NotShown = new("notShown");
}

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
    UnitDto Unit,
    string? ImageUrl,
    bool ShowOnWebsite)
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
        new UnitDto(p.Unit!.Id, p.Unit.Name, p.Unit.ShortName),
        p.ImageUrl,
        p.ShowOnWebsite);
}

/// <summary>
/// GET /api/products/generate-barcode → { "ok": true, "barcode": "20..." }: a free in-store EAN-13 to prefill
/// the product form. Not reserved; POST /api/products still rejects it if another product takes it first.
/// </summary>
public sealed record GeneratedBarcodeResponseDto(string Barcode) : SuccessResponse;

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
    UnitDto Unit,
    string? ImageUrl,
    bool ShowOnWebsite,
    StorefrontSyncDto Storefront) : SuccessResponse
{
    public static ProductResponseDto From(Product product, Category category, Unit unit, StorefrontSyncDto storefront) =>
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
            new UnitDto(unit.Id, unit.Name, unit.ShortName),
            product.ImageUrl,
            product.ShowOnWebsite,
            storefront);
}

/// <summary>
/// DELETE /api/products/{id} → { "ok": true, "deactivated", "message" }. <c>deactivated</c> is true when documents
/// still reference the product, so it was hidden from the catalog instead of removed.
/// </summary>
public sealed record ProductDeleteResponseDto(bool Deactivated, string Message) : SuccessResponse;
