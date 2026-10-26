using NexusERP.Application.Common;

namespace NexusERP.Application.Stores;

public interface IStoreService
{
    Task<Result<ItemsResponseDto<StoreDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<StoreResponseDto>> CreateAsync(string companyId, CreateStoreRequestDto request, CancellationToken cancellationToken);
    Task<Result<StoreResponseDto>> UpdateAsync(string companyId, string id, UpdateStoreRequestDto request, CancellationToken cancellationToken);
    Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
}
