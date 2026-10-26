using NexusERP.Application.Common;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Audit;

public sealed record AuditLogDto(
    string Id,
    DateTime CreatedAt,
    string? UserId,
    string UserName,
    string UserRole,
    string Department,
    AuditModule ModuleName,
    AuditAction ActionType,
    string Description,
    string? IpAddress);

/// <summary>GET /api/admin/audit-logs → { "ok": true, "items": [ ... ], "total", "page", "pageSize" }, newest first.</summary>
public sealed record AuditLogPageResponseDto(IReadOnlyList<AuditLogDto> Items, int Total, int Page, int PageSize) : SuccessResponse;
