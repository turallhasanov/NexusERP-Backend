using NexusERP.Application.Common;

namespace NexusERP.Application.Customers;

public interface ICustomerService
{
    Task<Result<ItemsResponseDto<CustomerDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<CustomerResponseDto>> CreateAsync(string companyId, CreateCustomerRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a counterparty, or deactivates it when purchases or sales reference it so those documents keep it. A
    /// counterparty the system itself created (e.g. the account online orders are booked to) is refused (400).
    /// </summary>
    Task<Result<CustomerDeleteResponseDto>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
}
