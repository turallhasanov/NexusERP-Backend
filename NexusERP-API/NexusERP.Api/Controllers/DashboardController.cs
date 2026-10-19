using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Dashboard;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    /// <summary>
    /// Company totals, optionally for a date range (same rules as the profit-loss report: a date-only
    /// <paramref name="to"/> includes that whole day). <paramref name="lowStockThreshold"/> defaults to 5.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType<DashboardSummaryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] decimal? lowStockThreshold,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken) =>
        ToActionResult(await dashboardService.GetSummaryAsync(CurrentCompanyId, lowStockThreshold, from, to, cancellationToken));
}
