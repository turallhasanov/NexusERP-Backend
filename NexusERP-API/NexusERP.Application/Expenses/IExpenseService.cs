using NexusERP.Application.Common;

namespace NexusERP.Application.Expenses;

public interface IExpenseService
{
    Task<Result<ItemsResponseDto<ExpenseDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<ExpenseResponseDto>> CreateAsync(string companyId, CreateExpenseRequestDto request, CancellationToken cancellationToken);
}
