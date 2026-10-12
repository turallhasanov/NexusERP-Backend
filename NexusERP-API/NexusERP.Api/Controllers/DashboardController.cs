using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Dashboard;

namespace NexusERP.Api.Controllers;

[Route("api/dashboard")]
[Authorize, RequireCompany]
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    /// <summary>Company totals. <paramref name="lowStockThreshold"/> defaults to 5.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] decimal? lowStockThreshold, CancellationToken cancellationToken) =>
        ToActionResult(await dashboardService.GetSummaryAsync(CurrentCompanyId, lowStockThreshold, cancellationToken));
}
