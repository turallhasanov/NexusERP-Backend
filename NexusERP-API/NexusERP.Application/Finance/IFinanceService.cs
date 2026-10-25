using NexusERP.Application.Common;

namespace NexusERP.Application.Finance;

public interface IFinanceService
{
    Task<Result<FinanceSummaryResponseDto>> GetSummaryAsync(string companyId, string? storeId, string? period, CancellationToken cancellationToken);
}
