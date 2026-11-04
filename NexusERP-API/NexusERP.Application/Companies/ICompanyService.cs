using NexusERP.Application.Common;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Companies;

public interface ICompanyService
{
    Task<Result<CurrentCompanyResponseDto>> GetForUserAsync(string userId, CancellationToken cancellationToken);
    Task<Result<CompanyResponseDto>> CreateAsync(string userId, CreateCompanyRequestDto request, CancellationToken cancellationToken);
    Task<Result<CompanyResponseDto>> JoinAsync(string userId, JoinCompanyRequestDto request, CancellationToken cancellationToken);
    Task<Result<MyCompanyResponseDto>> GetMyCompanyAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<ItemsResponseDto<CompanyUserDto>>> GetUsersAsync(string companyId, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates or reactivates a user of the company. A deactivated user cannot sign in, and the token they already
    /// hold is refused on its next request.
    /// </summary>
    Task<Result<CompanyUserResponseDto>> SetUserActiveAsync(
        string currentUserId, string companyId, string userId, SetUserStatusRequestDto request, CancellationToken cancellationToken);
    Task<Result<CompanyUserResponseDto>> UpdateUserRoleAsync(
        string currentUserId, string companyId, string userId, UpdateCompanyUserRoleRequestDto request, CancellationToken cancellationToken);

    /// <summary>The company's status, or null when it does not exist.</summary>
    Task<CompanyStatus?> GetStatusAsync(string companyId, CancellationToken cancellationToken);
}
