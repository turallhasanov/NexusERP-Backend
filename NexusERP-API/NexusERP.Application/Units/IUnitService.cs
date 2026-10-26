using NexusERP.Application.Common;

namespace NexusERP.Application.Units;

public interface IUnitService
{
    Task<Result<ItemsResponseDto<UnitDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<UnitResponseDto>> CreateAsync(string companyId, CreateUnitRequestDto request, CancellationToken cancellationToken);
    Task<Result<UnitResponseDto>> UpdateAsync(string companyId, string id, UpdateUnitRequestDto request, CancellationToken cancellationToken);
}
