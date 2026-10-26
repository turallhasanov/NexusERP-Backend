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

    [HttpPost("{id}/approve")]
    [ProducesResponseType<PurchaseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.ApproveAsync(CurrentCompanyId, id, cancellationToken));
}
