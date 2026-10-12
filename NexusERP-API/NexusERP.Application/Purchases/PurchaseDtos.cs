using System.Linq.Expressions;
using NexusERP.Application.Common;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Purchases;

/// <summary>
/// Fields are nullable so missing values reach our own validation instead of the framework's.
/// <c>documentDate</c> defaults to now.
/// </summary>
public sealed record CreatePurchaseRequestDto(
    string? WarehouseId,
    string? CustomerId,
    DateTime? DocumentDate,
    IReadOnlyList<PurchaseItemRequestDto?>? Items);

public sealed record PurchaseItemRequestDto(string? ProductId, decimal? Quantity, decimal? UnitPrice);

/// <summary>Row in GET /api/purchases.</summary>
public sealed record PurchaseSummaryDto(
    string Id,
    string DocumentNumber,
    DateTime DocumentDate,
    PurchaseStatus Status,
    decimal TotalAmount,
    string WarehouseId,
    CodeNameDto Warehouse,
    string CustomerId,
    CodeNameDto Customer,
    int ItemCount);

public sealed record PurchaseItemDto(
    string Id,
    int LineNumber,
    string ProductId,
    ProductRefDto Product,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

/// <summary>Full document with lines.</summary>
public sealed record PurchaseDto(
    string Id,
    string DocumentNumber,
    DateTime DocumentDate,
    PurchaseStatus Status,
    decimal TotalAmount,
    string WarehouseId,
    CodeNameDto Warehouse,
    string CustomerId,
    CodeNameDto Customer,
    IReadOnlyList<PurchaseItemDto> Items)
{
    /// <summary>Server-side projection, loads header, references and lines in one query.</summary>
    public static readonly Expression<Func<Purchase, PurchaseDto>> Projection = p => new PurchaseDto(
        p.Id,
        p.DocumentNumber,
        p.DocumentDate,
        p.Status,
        p.TotalAmount,
        p.WarehouseId,
        new CodeNameDto(p.Warehouse!.Id, p.Warehouse.Code, p.Warehouse.Name),
        p.CustomerId,
        new CodeNameDto(p.Customer!.Id, p.Customer.Code, p.Customer.Name),
        p.Items
            .OrderBy(i => i.LineNumber)
            .Select(i => new PurchaseItemDto(
                i.Id,
                i.LineNumber,
                i.ProductId,
                new ProductRefDto(i.Product!.Id, i.Product.Code, i.Product.Name, i.Product.Barcode, i.Product.Unit!.ShortName),
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice))
            .ToList());
}

/// <summary>GET /api/purchases/{id} → { "ok": true, "purchase": { ... } }</summary>
public sealed record PurchaseLookupResponseDto(PurchaseDto Purchase) : SuccessResponse;

/// <summary>POST /api/purchases and POST /api/purchases/{id}/approve → { "ok": true, "id", "documentNumber", ..., "items": [ ... ] }</summary>
public sealed record PurchaseResponseDto(
    string Id,
    string DocumentNumber,
    DateTime DocumentDate,
    PurchaseStatus Status,
    decimal TotalAmount,
    string WarehouseId,
    CodeNameDto Warehouse,
    string CustomerId,
    CodeNameDto Customer,
    IReadOnlyList<PurchaseItemDto> Items) : SuccessResponse
{
    public static PurchaseResponseDto From(PurchaseDto purchase) =>
        new(
            purchase.Id,
            purchase.DocumentNumber,
            purchase.DocumentDate,
            purchase.Status,
            purchase.TotalAmount,
            purchase.WarehouseId,
            purchase.Warehouse,
            purchase.CustomerId,
            purchase.Customer,
            purchase.Items);
}
