using NexusERP.Application.Common;

namespace NexusERP.Application.Categories;

public interface ICategoryService
{
    Task<Result<ItemsResponseDto<CategoryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<CategoryResponseDto>> CreateAsync(string companyId, CreateCategoryRequestDto request, CancellationToken cancellationToken);
    Task<Result<CategoryResponseDto>> UpdateAsync(string companyId, string id, UpdateCategoryRequestDto request, CancellationToken cancellationToken);
    Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
}
