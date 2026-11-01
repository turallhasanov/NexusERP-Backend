using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Purchases;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class PurchasesController(IPurchaseService purchaseService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<PurchaseSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("{id}")]
    [ProducesResponseType<PurchaseLookupResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.GetByIdAsync(CurrentCompanyId, id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<PurchaseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>
    /// Cancels a purchase (status "cancelled") and takes the quantities it added back out of the warehouse stock. Refused
    /// with 422 when part of that stock is already gone, and with 409 when a cash payment is linked to the purchase.
    /// <c>?force=true</c> cancels it anyway: each balance drops by what is there and ends at zero, never below.
    /// Company admins and IT admins only.
    /// </summary>
    /// <param name="id">Purchase identifier.</param>
    /// <param name="force">Skip the part of the stock that is no longer in the warehouse instead of refusing.</param>
    /// <param name="cancellationToken">Aborts the request when the client disconnects.</param>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<PurchaseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(string id, [FromQuery] bool force, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.CancelAsync(CurrentCompanyId, id, force, cancellationToken));

    [HttpPost("{id}/approve")]
    [ProducesResponseType<PurchaseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.ApproveAsync(CurrentCompanyId, id, cancellationToken));
}
