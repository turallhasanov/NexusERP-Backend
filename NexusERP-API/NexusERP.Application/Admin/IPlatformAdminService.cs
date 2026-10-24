using NexusERP.Application.Common;
using NexusERP.Application.Dashboard;

namespace NexusERP.Application.Admin;

/// <summary>Cross-company operations for platform administrators (role "superadmin"). Never scoped to one company.</summary>
public interface IPlatformAdminService
{
    Task<Result<ItemsResponseDto<TenantDto>>> GetTenantsAsync(CancellationToken cancellationToken);
    Task<Result<TenantDetailsResponseDto>> GetTenantAsync(string tenantId, CancellationToken cancellationToken);
    Task<Result<CreateTenantResponseDto>> CreateTenantAsync(CreateTenantRequestDto request, CancellationToken cancellationToken);
    Task<Result<TenantResponseDto>> UpdateStatusAsync(string tenantId, UpdateTenantStatusRequestDto request, CancellationToken cancellationToken);

    Task<Result<DashboardSummaryResponseDto>> GetTenantDashboardSummaryAsync(
        string tenantId, decimal? lowStockThreshold, DateTime? from, DateTime? to, CancellationToken cancellationToken);

    Task<Result<ImpersonationResponseDto>> ImpersonateAsync(string superAdminId, string tenantId, CancellationToken cancellationToken);
    Task<Result<PlatformAnalyticsResponseDto>> GetAnalyticsAsync(CancellationToken cancellationToken);
}
