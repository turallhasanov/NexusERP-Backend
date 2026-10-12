using NexusERP.Application.Common;

namespace NexusERP.Application.Companies;

public interface ICompanyService
{
    Task<Result<CurrentCompanyResponseDto>> GetForUserAsync(string userId, CancellationToken cancellationToken);
    Task<Result<CompanyResponseDto>> CreateAsync(string userId, CreateCompanyRequestDto request, CancellationToken cancellationToken);
}
