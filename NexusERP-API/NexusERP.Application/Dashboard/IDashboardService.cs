using NexusERP.Application.Common;

namespace NexusERP.Application.Dashboard;

public interface IDashboardService
{
    Task<Result<DashboardSummaryResponseDto>> GetSummaryAsync(string companyId, decimal? lowStockThreshold, CancellationToken cancellationToken);
}
