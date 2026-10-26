using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Audit;
using NexusERP.Application.Common;
using NexusERP.Domain.Enums;

namespace NexusERP.Api.Controllers;

/// <summary>
/// The company's audit trail. IT admins ("itadmin") only: company admins, staff and platform admins viewing the
/// company get 403. Every entry belongs to the caller's own company.
/// </summary>
[Route("api/admin/audit-logs")]
[Authorize(Roles = AppRoles.ITAdmin), RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class AuditLogsController(IAuditLogService auditLogService) : ApiControllerBase
{
    /// <summary>
    /// Newest first, 50 per page (<paramref name="pageSize"/> up to 200). <paramref name="department"/>: Maliyyə, İHR,
    /// Anbar or Şəbəkə; <paramref name="moduleName"/>: finance, hr, inventory, retail; <paramref name="actionType"/>:
    /// create, update, delete; <paramref name="startDate"/>/<paramref name="endDate"/>: whole days (YYYY-MM-DD).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<AuditLogPageResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] string? department,
        [FromQuery] AuditModule? moduleName,
        [FromQuery] AuditAction? actionType,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken) =>
        ToActionResult(await auditLogService.GetAsync(
            CurrentCompanyId, department, moduleName, actionType, startDate, endDate, page, pageSize, cancellationToken));
}
