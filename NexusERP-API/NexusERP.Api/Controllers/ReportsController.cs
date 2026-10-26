using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Reports;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class ReportsController(IReportService reportService) : ApiControllerBase
{
    [HttpGet("inventory-value")]
    [ProducesResponseType<ItemsResponseDto<InventoryReportDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryValue(CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetInventoryValueAsync(CurrentCompanyId, cancellationToken));

    /// <summary>Best sellers. <paramref name="limit"/> defaults to 10 (max 100); <paramref name="sortBy"/> is "quantity" (default) or "revenue".</summary>
    [HttpGet("top-products")]
    [ProducesResponseType<ItemsResponseDto<TopProductReportDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopProducts([FromQuery] int? limit, [FromQuery] string? sortBy, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetTopProductsAsync(CurrentCompanyId, limit, sortBy, cancellationToken));

    /// <summary>
    /// Net profit over approved sales and recorded expenses. Both bounds are optional; a date-only
    /// <paramref name="to"/> includes that whole day.
    /// </summary>
    [HttpGet("profit-loss")]
    [ProducesResponseType<ProfitLossResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProfitLoss([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken) =>
        ToActionResult(await reportService.GetProfitLossAsync(CurrentCompanyId, from, to, cancellationToken));
}
