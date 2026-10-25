using NexusERP.Application.Common;

namespace NexusERP.Application.Employees;

public interface IEmployeeService
{
    Task<Result<ItemsResponseDto<EmployeeDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<EmployeeResponseDto>> CreateAsync(string companyId, CreateEmployeeRequestDto request, CancellationToken cancellationToken);
    Task<Result<EmployeeResponseDto>> UpdateAsync(string companyId, string id, UpdateEmployeeRequestDto request, CancellationToken cancellationToken);
    Task<Result<EmployeeResponseDto>> SendOnVacationAsync(string companyId, string id, VacationRequestDto request, CancellationToken cancellationToken);
    Task<Result<EmployeeResponseDto>> ReturnFromVacationAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<EmployeeResponseDto>> TerminateAsync(string companyId, string id, TerminateEmployeeRequestDto request, CancellationToken cancellationToken);
    Task<Result<TerminationDocumentResponseDto>> GetTerminationDocumentAsync(string companyId, string id, CancellationToken cancellationToken);
}
