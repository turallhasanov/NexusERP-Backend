using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Audit;

public class AuditLogService(IApplicationDbContext db) : IAuditLogService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    // Date filters are Azerbaijani calendar days: UTC+4 all year.
    private static readonly TimeSpan BakuOffset = TimeSpan.FromHours(4);

    public async Task<Result<AuditLogPageResponseDto>> GetAsync(
        string companyId,
        string? department,
        AuditModule? moduleName,
        AuditAction? actionType,
        DateOnly? startDate,
        DateOnly? endDate,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        if (startDate > endDate)
            return Result<AuditLogPageResponseDto>.Failure(ErrorType.Validation, "Başlanğıc tarixi son tarixdən sonra ola bilməz.");

        if (moduleName is { } module && !Enum.IsDefined(module))
            return Result<AuditLogPageResponseDto>.Failure(ErrorType.Validation, "Modul yanlışdır. Mümkün dəyərlər: finance, hr, inventory, retail.");

        if (actionType is { } action && !Enum.IsDefined(action))
            return Result<AuditLogPageResponseDto>.Failure(ErrorType.Validation, "Əməliyyat növü yanlışdır. Mümkün dəyərlər: create, update, delete.");

        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        var number = Math.Max(page ?? 1, 1);

        var query = db.AuditLogs.AsNoTracking().Where(a => a.CompanyId == companyId);

        if (!string.IsNullOrWhiteSpace(department))
        {
            var value = department.Trim();
            query = query.Where(a => a.Department == value);
        }

        if (moduleName is not null)
            query = query.Where(a => a.ModuleName == moduleName);

        if (actionType is not null)
            query = query.Where(a => a.ActionType == actionType);

        // Whole days: from the start of startDate to the end of endDate, Baku time.
        if (startDate is { } start)
        {
            var from = start.ToDateTime(TimeOnly.MinValue) - BakuOffset;
            query = query.Where(a => a.CreatedAt >= from);
        }

        if (endDate is { } end)
        {
            var to = end.AddDays(1).ToDateTime(TimeOnly.MinValue) - BakuOffset;
            query = query.Where(a => a.CreatedAt < to);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((number - 1) * size)
            .Take(size)
            .Select(a => new AuditLogDto(
                a.Id, a.CreatedAt, a.UserId, a.UserName, a.UserRole, a.Department, a.ModuleName, a.ActionType, a.Description, a.IpAddress))
            .ToListAsync(cancellationToken);

        return new AuditLogPageResponseDto(items, total, number, size);
    }
}
