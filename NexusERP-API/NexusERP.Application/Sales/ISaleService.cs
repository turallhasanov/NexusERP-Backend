using NexusERP.Application.Common;

namespace NexusERP.Application.Sales;

public interface ISaleService
{
    /// <summary>
    /// The company's sales. <paramref name="customerId"/> keeps one counterparty's sales; <paramref name="search"/> keeps the
    /// sales whose customer name, code or phone (counterparty or online shopper) or document number contains the text.
    /// </summary>
    Task<Result<ItemsResponseDto<SaleSummaryDto>>> GetAllAsync(
        string companyId, string? customerId, string? search, CancellationToken cancellationToken);
    Task<Result<SaleLookupResponseDto>> GetByIdAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> CreateAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> ApproveAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> PosCheckoutAsync(string companyId, CreateSaleRequestDto request, CancellationToken cancellationToken);
    Task<Result<SaleResponseDto>> CreateOnlineOrderAsync(string companyId, CreateOnlineOrderRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a sale: its items return to the warehouse stock, an online (Khazar) order is cancelled in Khazar first,
    /// and a POS payment is taken back out of its cash register. Cancelling a cancelled sale succeeds unchanged.
    /// </summary>
    Task<Result<SaleResponseDto>> CancelAsync(string companyId, string id, CancellationToken cancellationToken);

    /// <summary>
    /// Cancels the sale created for an online order and returns its items to the warehouse stock. Cancelling an
    /// already cancelled order succeeds without changing stock again. Khazar asked for this, so it is not told again.
    /// </summary>
    Task<Result<SaleResponseDto>> CancelOnlineOrderAsync(string companyId, string externalOrderNumber, CancellationToken cancellationToken);
}
