using NexusERP.Application.Common;

namespace NexusERP.Application.Reports;

public interface IReportService
{
    Task<Result<ItemsResponseDto<InventoryReportDto>>> GetInventoryValueAsync(string companyId, CancellationToken cancellationToken);
    Task<Result<ItemsResponseDto<TopProductReportDto>>> GetTopProductsAsync(string companyId, int? limit, string? sortBy, CancellationToken cancellationToken);
}
