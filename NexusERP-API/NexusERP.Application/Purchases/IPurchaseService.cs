using NexusERP.Application.Common;

namespace NexusERP.Application.Purchases;

public interface IPurchaseService
{
    Task<Result<ItemsResponseDto<PurchaseSummaryDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<PurchaseLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<PurchaseResponseDto>> CreateAsync(string companyId, CreatePurchaseRequestDto request, CancellationToken cancellationToken);
    Task<Result<PurchaseResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a purchase: the quantities it added are taken back out of the warehouse stock. Refused (and nothing changes)
    /// when part of that stock is already gone (StockShortage) or a cash payment is linked to the purchase. With
    /// <paramref name="force"/> the missing part is skipped instead: each balance drops by what is there, never below zero.
    /// Cancelling a cancelled purchase succeeds unchanged.
    /// </summary>
    Task<Result<PurchaseResponseDto>> CancelAsync(string companyId, string id, bool force, CancellationToken cancellationToken);
}
