using NexusERP.Application.Common;

namespace NexusERP.Application.Purchases;

public interface IPurchaseService
{
    Task<Result<ItemsResponseDto<PurchaseSummaryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<PurchaseLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<PurchaseResponseDto>> CreateAsync(string companyId, CreatePurchaseRequestDto request, CancellationToken cancellationToken);
    Task<Result<PurchaseResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken);
}
