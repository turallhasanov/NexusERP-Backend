using NexusERP.Application.Common;

namespace NexusERP.Application.Sales;

public interface ISaleService
{
    Task<Result<ItemsResponseDto<SaleSummaryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<SaleLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> CreateAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> PosCheckoutAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken);
}
