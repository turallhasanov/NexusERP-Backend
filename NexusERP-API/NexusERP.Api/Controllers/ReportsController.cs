using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Reports;

namespace NexusERP.Api.Controllers;

[Route("api/reports")]
[Authorize, RequireCompany]
public class ReportsController(IReportService reportService) : ApiControllerBase
{
    [HttpGet("inventory-value")]
    public async Task<IActionResult> GetInventoryValue(CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetInventoryValueAsync(CurrentCompanyId, cancellationToken));

    /// <summary>Best sellers. <paramref name="limit"/> defaults to 10 (max 100); <paramref name="sortBy"/> is "quantity" (default) or "revenue".</summary>
    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] int? limit, [FromQuery] string? sortBy, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetTopProductsAsync(CurrentCompanyId, limit, sortBy, cancellationToken));

    /// <summary>
    /// Net profit over approved sales and recorded expenses. Both bounds are optional; a date-only
    /// <paramref name="to"/> includes that whole day.
    /// </summary>
    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitLoss([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetProfitLossAsync(CurrentCompanyId, from, to, cancellationToken));
}
