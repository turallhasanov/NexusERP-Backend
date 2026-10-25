using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Finance;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class FinanceController(IFinanceService financeService) : ApiControllerBase
{
    /// <summary>
    /// Profit and loss for today (<c>daily</c>), this month (<c>monthly</c>, default) or this year so far
    /// (<c>yearly</c>), in Azerbaijani time. With <paramref name="storeId"/>: that store's sales, cost of goods sold,
    /// expenses tagged with it and salaries of staff assigned to it.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType<FinanceSummaryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSummary([FromQuery] string? storeId, [FromQuery] string? period, CancellationToken cancellationToken) =>
        ToActionResult(await financeService.GetSummaryAsync(CurrentCompanyId, storeId, period, cancellationToken));
}
