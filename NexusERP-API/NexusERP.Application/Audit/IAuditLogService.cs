using NexusERP.Application.Common;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Audit;

public interface IAuditLogService
{
    Task<Result<AuditLogPageResponseDto>> GetAsync(
        string companyId,
        string? department,
        AuditModule? moduleName,
        AuditAction? actionType,
        DateOnly? startDate,
        DateOnly? endDate,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken);
}
