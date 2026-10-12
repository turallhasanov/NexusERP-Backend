using NexusERP.Application.Common;

namespace NexusERP.Application.Warehouses;

public interface IWarehouseService
{
    Task<Result<ItemsResponseDto<WarehouseDto>>> GetActiveAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<WarehouseResponseDto>> CreateAsync(string companyId, CreateWarehouseRequestDto request, CancellationToken cancellationToken);
}
