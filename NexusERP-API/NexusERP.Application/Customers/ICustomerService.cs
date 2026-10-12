using NexusERP.Application.Common;

namespace NexusERP.Application.Customers;

public interface ICustomerService
{
    Task<Result<ItemsResponseDto<CustomerDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<CustomerResponseDto>> CreateAsync(string companyId, CreateCustomerRequestDto request, CancellationToken cancellationToken);
}
