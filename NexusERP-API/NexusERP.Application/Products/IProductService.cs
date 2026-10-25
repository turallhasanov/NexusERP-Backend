using NexusERP.Application.Common;

namespace NexusERP.Application.Products;

public interface IProductService
{
    Task<Result<ItemsResponseDto<ProductDto>>> GetAllAsync(string companyId, string? q, string? categoryId, CancellationToken cancellationToken);
    Task<Result<GeneratedBarcodeResponseDto>> GenerateBarcodeAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<ProductLookupResponseDto>> GetByBarcodeAsync(string companyId, string barcode, CancellationToken cancellationToken);
    Task<Result<ProductResponseDto>> CreateAsync(string companyId, CreateProductRequestDto request, CancellationToken cancellationToken);
    Task<Result<ProductDeleteResponseDto>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
}
