using NexusERP.Application.Common;

namespace NexusERP.Application.Stores;

public interface IStoreService
{
    Task<Result<ItemsResponseDto<StoreDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<StoreResponseDto>> CreateAsync(string companyId, CreateStoreRequestDto request, CancellationToken cancellationToken);
}
