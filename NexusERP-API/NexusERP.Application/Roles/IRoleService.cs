using NexusERP.Application.Common;
using NexusERP.Application.Companies;

namespace NexusERP.Application.Roles;

public interface IRoleService
{
    Task<Result<ItemsResponseDto<RoleDto>>> GetAllAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<RoleResponseDto>> CreateAsync(string companyId, CreateRoleRequestDto request, CancellationToken cancellationToken);
    Task<Result<RoleResponseDto>> UpdateAsync(string companyId, string id, UpdateRoleRequestDto request, CancellationToken cancellationToken);
    Task<Result<SuccessResponse>> DeleteAsync(string companyId, string id, CancellationToken cancellationToken);
    Task<Result<CompanyUserResponseDto>> AssignAsync(string companyId, string userId, AssignRoleRequestDto request, CancellationToken cancellationToken);
}
