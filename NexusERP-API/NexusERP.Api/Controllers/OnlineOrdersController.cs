using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Sales;

namespace NexusERP.Api.Controllers;

/// <summary>
/// Retail (B2C) orders from the Khazar online store. They become draft sales with channel "online", booked to the
/// company's "Khazar Online Müştəri" account, with the end customer's details on the document; they never go
/// through the wholesale sales form. Khazar calls this with its NexusERP service account.
/// </summary>
[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class OnlineOrdersController(ISaleService saleService) : ApiControllerBase
{
    /// <summary>Creates the draft sale, or returns the one already created for this <c>externalOrderNumber</c>.</summary>
    [HttpPost]
    [ProducesResponseType<SaleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateOnlineOrderRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.CreateOnlineOrderAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>
    /// Cancels the online order's sale (status "cancelled") and returns its items to the warehouse stock.
    /// Repeating the call for an already cancelled order succeeds and leaves stock as it is.
    /// </summary>
    [HttpDelete("{externalOrderNumber}")]
    [ProducesResponseType<SaleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(string externalOrderNumber, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.CancelOnlineOrderAsync(CurrentCompanyId, externalOrderNumber, cancellationToken));
}
