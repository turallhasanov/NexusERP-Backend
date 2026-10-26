using NexusERP.Application.Common;

namespace NexusERP.Application.Warehouses;

public interface IWarehouseService
{
    Task<Result<ItemsResponseDto<WarehouseDto>>> GetActiveAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<WarehouseDetailsResponseDto>> GetDetailsAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<WarehouseResponseDto>> CreateAsync(string companyId, CreateWarehouseRequestDto request, CancellationToken cancellationToken);
    Task<Result<WarehouseResponseDto>> UpdateAsync(string companyId, string id, UpdateWarehouseRequestDto request, CancellationToken cancellationToken);
    Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
}
