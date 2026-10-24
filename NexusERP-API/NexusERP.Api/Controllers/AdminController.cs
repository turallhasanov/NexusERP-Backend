using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Application.Admin;
using NexusERP.Application.Common;
using NexusERP.Application.Dashboard;

namespace NexusERP.Api.Controllers;

/// <summary>
/// Platform administration across every company (tenant). Only the "superadmin" role gets in; company
/// admins, staff and impersonation tokens (role "admin") all get 403.
/// </summary>
[Route("api/admin")]
[Authorize(Roles = AppRoles.SuperAdmin)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class AdminController(IPlatformAdminService adminService) : ApiControllerBase
{
    /// <summary>Every company, newest first, with its status and user count.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType<ItemsResponseDto<TenantDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants(CancellationToken cancellationToken) =>
        ToActionResult(await adminService.GetTenantsAsync(cancellationToken));

    /// <summary>Company profile, record counts, users and latest documents.</summary>
    [HttpGet("tenants/{id}")]
    [ProducesResponseType<TenantDetailsResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(string id, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.GetTenantAsync(id, cancellationToken));

    /// <summary>Creates a company together with its first admin account (already confirmed).</summary>
    [HttpPost("tenants")]
    [ProducesResponseType<CreateTenantResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.CreateTenantAsync(request, cancellationToken));

    /// <summary>Activates, deactivates (passive) or suspends a company. Its users are signed out on their next request.</summary>
    [HttpPut("tenants/{id}/status")]
    [ProducesResponseType<TenantResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenantStatus(string id, [FromBody] UpdateTenantStatusRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.UpdateStatusAsync(id, request, cancellationToken));

    /// <summary>The company's dashboard figures; same query parameters and rules as GET /api/dashboard/summary.</summary>
    [HttpGet("tenants/{id}/dashboard-summary")]
    [ProducesResponseType<DashboardSummaryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenantDashboardSummary(
        string id,
        [FromQuery] decimal? lowStockThreshold,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken) =>
        ToActionResult(await adminService.GetTenantDashboardSummaryAsync(id, lowStockThreshold, from, to, cancellationToken));

    /// <summary>
    /// A short-lived, read-only token for the company's regular endpoints, to see the company as its admin does.
    /// Writes with it get 403; the caller keeps its own token to come back.
    /// </summary>
    [HttpPost("tenants/{id}/impersonate")]
    [ProducesResponseType<ImpersonationResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Impersonate(string id, CancellationToken cancellationToken) =>
        ToActionResult(await adminService.ImpersonateAsync(CurrentUserId, id, cancellationToken));

    /// <summary>Platform totals: companies by status, users, revenue, top companies and database health.</summary>
    [HttpGet("analytics")]
    [ProducesResponseType<PlatformAnalyticsResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetAnalytics(CancellationToken cancellationToken) =>
        ToActionResult(await adminService.GetAnalyticsAsync(cancellationToken));
}
