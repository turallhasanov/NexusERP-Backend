using NexusERP.Application.Common;

namespace NexusERP.Application.Integration;

/// <summary>
/// One Khazar product variant. <paramref name="Sku"/> becomes the NexusERP product's barcode. <paramref name="ImageUrl"/>
/// is the photo's full address on the Khazar API (the client makes Khazar's relative paths absolute), or null.
/// </summary>
public sealed record KhazarCatalogItem(
    string Sku, string Name, string? Category, string? Size, string? Color, decimal Price, int Stock, bool IsActive,
    string? ImageUrl = null);

/// <summary>Either the catalog or, when it could not be read, a user-facing (Azerbaijani) reason.</summary>
public sealed record KhazarCatalogResult(IReadOnlyList<KhazarCatalogItem>? Items, string? Error);

/// <summary>Checks Khazar's health, then reads its storefront catalog. Never throws on connection failures.</summary>
public interface IKhazarCatalogClient
{
    Task<KhazarCatalogResult> GetCatalogAsync(CancellationToken cancellationToken);
}

/// <summary>Outcome of cancelling an order in Khazar: <see cref="Error"/> is an Azerbaijani reason when it failed.</summary>
public sealed record KhazarCancelResult(bool Success, string? Error)
{
    public static readonly KhazarCancelResult Ok = new(true, null);

    public static KhazarCancelResult Failed(string error) => new(false, error);
}

/// <summary>Tells Khazar that an online order was cancelled here, so it cancels the order and returns its stock.</summary>
public interface IKhazarOrderClient
{
    /// <summary>
    /// DELETE api/integration/orders/{orderNumber}. Succeeds when Khazar cancelled the order, had already cancelled it,
    /// or does not know it. Never throws on connection failures.
    /// </summary>
    Task<KhazarCancelResult> CancelOrderAsync(string orderNumber, CancellationToken cancellationToken);
}

/// <summary>A product as it is sent to the Khazar store. <paramref name="Sku"/> is the product's barcode.</summary>
public sealed record KhazarProductPush(
    string Sku, string Name, string? CategoryName, decimal SalePrice, int Stock, bool IsPublished)
{
    /// <summary>
    /// Further photos sent once with the product (they are not kept here; the store's gallery holds them). They follow
    /// the main image, or, with no main image in this push, the photos the store already has.
    /// </summary>
    public IReadOnlyList<KhazarGalleryImage>? Gallery { get; init; }
}

/// <summary>A gallery photo's file name and bytes.</summary>
public sealed record KhazarGalleryImage(string FileName, byte[] Content);

/// <summary>Outcome of sending a product to Khazar: <see cref="Error"/> is an Azerbaijani reason when it failed.</summary>
public sealed record KhazarProductResult(bool Success, string? Error)
{
    public static readonly KhazarProductResult Ok = new(true, null);

    public static KhazarProductResult Failed(string error) => new(false, error);
}

/// <summary>Sends products (with their photo) to the Khazar online store.</summary>
public interface IKhazarProductClient
{
    /// <summary>
    /// POST api/integration/products. Khazar adds the product, or updates it by barcode, and shows or hides it as
    /// <see cref="KhazarProductPush.IsPublished"/> says. <paramref name="image"/>, when given, becomes its first photo.
    /// Never throws on connection failures.
    /// </summary>
    Task<KhazarProductResult> PushProductAsync(
        KhazarProductPush product, Stream? image, string? imageFileName, CancellationToken cancellationToken);
}

/// <summary>POST /api/nexuserp/reset-stock → how many stock balances were set to 0.</summary>
public sealed record ResetStockResponseDto(int BalancesReset) : SuccessResponse;

public interface ICatalogSyncService
{
    /// <summary>Sets every stock balance of the company (all warehouses) to 0. Products and documents are untouched.</summary>
    Task<Result<ResetStockResponseDto>> ResetStockAsync(string companyId, CancellationToken cancellationToken);

    /// <summary>Upserts every active Khazar variant as a NexusERP product and sets its stock to Khazar's.</summary>
    Task<Result<CatalogSyncResponseDto>> SyncAllAsync(string companyId, CancellationToken cancellationToken);
}

/// <summary>POST /api/nexuserp/sync-all-data → counts of what the run did; <c>errors</c> lists skipped variants.</summary>
public sealed record CatalogSyncResponseDto(
    int Fetched, int Created, int Updated, int Unchanged, int StockAdjusted, int Skipped, IReadOnlyList<string> Errors)
    : SuccessResponse;
