using System.Linq.Expressions;
using NexusERP.Application.Common;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Sales;

/// <summary>
/// Body of POST /api/sales and POST /api/sales/pos-checkout. Fields are nullable so missing values reach our
/// own validation. <c>warehouseId</c> defaults to the store's warehouse, <c>customerId</c> may be omitted for
/// walk-in customers, and <c>documentDate</c> defaults to now.
/// </summary>
public sealed record CreateSaleRequestDto(
    string? StoreId,
    string? WarehouseId,
    string? CustomerId,
    DateTime? DocumentDate,
    IReadOnlyList<SaleItemRequestDto?>? Items);

/// <summary><c>unitPrice</c> defaults to the product's current sale price.</summary>
public sealed record SaleItemRequestDto(string? ProductId, decimal? Quantity, decimal? UnitPrice);

/// <summary>Row in GET /api/sales.</summary>
public sealed record SaleSummaryDto(
    string Id,
    string DocumentNumber,
    DateTime DocumentDate,
    SaleStatus Status,
    decimal TotalAmount,
    string StoreId,
    CodeNameDto Store,
    string WarehouseId,
    CodeNameDto Warehouse,
    string? CustomerId,
    CodeNameDto? Customer,
    int ItemCount);

public sealed record SaleItemDto(
    string Id,
    int LineNumber,
    string ProductId,
    ProductRefDto Product,
    decimal Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

/// <summary>Full document with lines.</summary>
public sealed record SaleDto(
    string Id,
    string DocumentNumber,
    DateTime DocumentDate,
    SaleStatus Status,
    decimal TotalAmount,
    string StoreId,
    CodeNameDto Store,
    string WarehouseId,
    CodeNameDto Warehouse,
    string? CustomerId,
    CodeNameDto? Customer,
    IReadOnlyList<SaleItemDto> Items)
{
    /// <summary>Server-side projection, loads header, references and lines in one query.</summary>
    public static readonly Expression<Func<Sale, SaleDto>> Projection = s => new SaleDto(
        s.Id,
        s.DocumentNumber,
        s.DocumentDate,
        s.Status,
        s.TotalAmount,
        s.StoreId,
        new CodeNameDto(s.Store!.Id, s.Store.Code, s.Store.Name),
        s.WarehouseId,
        new CodeNameDto(s.Warehouse!.Id, s.Warehouse.Code, s.Warehouse.Name),
        s.CustomerId,
        s.CustomerId == null ? null : new CodeNameDto(s.Customer!.Id, s.Customer.Code, s.Customer.Name),
        s.Items
            .OrderBy(i => i.LineNumber)
            .Select(i => new SaleItemDto(
                i.Id,
                i.LineNumber,
                i.ProductId,
                new ProductRefDto(i.Product!.Id, i.Product.Code, i.Product.Name, i.Product.Barcode, i.Product.Unit!.ShortName),
                i.Quantity,
                i.UnitPrice,
                i.TotalPrice))
            .ToList());
}

/// <summary>GET /api/sales/{id} → { "ok": true, "sale": { ... } }</summary>
public sealed record SaleLookupResponseDto(SaleDto Sale) : SuccessResponse;

/// <summary>POST /api/sales, /{id}/approve and /pos-checkout → { "ok": true, "id", "documentNumber", ..., "items": [ ... ] }</summary>
public sealed record SaleResponseDto(
    string Id,
    string DocumentNumber,
    DateTime DocumentDate,
    SaleStatus Status,
    decimal TotalAmount,
    string StoreId,
    CodeNameDto Store,
    string WarehouseId,
    CodeNameDto Warehouse,
    string? CustomerId,
    CodeNameDto? Customer,
    IReadOnlyList<SaleItemDto> Items) : SuccessResponse
{
    public static SaleResponseDto From(SaleDto sale) =>
        new(
            sale.Id,
            sale.DocumentNumber,
            sale.DocumentDate,
            sale.Status,
            sale.TotalAmount,
            sale.StoreId,
            sale.Store,
            sale.WarehouseId,
            sale.Warehouse,
            sale.CustomerId,
            sale.Customer,
            sale.Items);
}
