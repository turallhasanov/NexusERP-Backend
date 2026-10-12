using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Dashboard;

namespace NexusERP.Api.Controllers;

[Route("api/dashboard")]
[Authorize, RequireCompany]
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    /// <summary>
    /// Company totals, optionally for a date range (same rules as the profit-loss report: a date-only
    /// <paramref name="to"/> includes that whole day). <paramref name="lowStockThreshold"/> defaults to 5.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] decimal? lowStockThreshold,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken) =>
        ToActionResult(await dashboardService.GetSummaryAsync(CurrentCompanyId, lowStockThreshold, from, to, cancellationToken));
}
