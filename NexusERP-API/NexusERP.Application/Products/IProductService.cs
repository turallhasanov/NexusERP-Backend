using NexusERP.Application.Common;

namespace NexusERP.Application.Products;

public interface IProductService
{
    Task<Result<ItemsResponseDto<ProductDto>>> GetAllAsync(string companyId, string? q, string? categoryId, CancellationToken cancellationToken);
    Task<Result<GeneratedBarcodeResponseDto>> GenerateBarcodeAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<ProductLookupResponseDto>> GetByBarcodeAsync(string companyId, string barcode, CancellationToken cancellationToken);
    /// <summary>
    /// Adds a product. <paramref name="imageUrl"/> is the site path of a photo the caller already stored (null for none).
    /// When <see cref="CreateProductRequestDto.ShowOnWebsite"/> is set the product is also sent to the Khazar store; if that
    /// fails the product is still saved and the response says so in <c>storefront</c>.
    /// </summary>
    Task<Result<ProductResponseDto>> CreateAsync(string companyId, CreateProductRequestDto request, string? imageUrl, CancellationToken cancellationToken,
        IReadOnlyList<string>? galleryImageUrls = null);

    /// <summary>
    /// Changes a product. <paramref name="newImageUrl"/> replaces its photo; <paramref name="removeImage"/> drops it. The
    /// Khazar store is updated when the product is, or just was, shown there.
    /// </summary>
    Task<Result<ProductResponseDto>> UpdateAsync(
        string companyId, string id, UpdateProductRequestDto request, string? newImageUrl, bool removeImage, CancellationToken cancellationToken,
        IReadOnlyList<string>? galleryImageUrls = null);
    Task<Result<ProductDeleteResponseDto>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
}
