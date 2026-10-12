using NexusERP.Application.Common;

namespace NexusERP.Application.Expenses;

public interface IExpenseCategoryService
{
    Task<Result<ItemsResponseDto<ExpenseCategoryDto>>> GetActiveAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<ExpenseCategoryResponseDto>> CreateAsync(string companyId, CreateExpenseCategoryRequestDto request, CancellationToken cancellationToken);
}
