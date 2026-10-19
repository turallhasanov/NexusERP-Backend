using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Sales;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class SalesController(ISaleService saleService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<SaleSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await saleService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("{id}")]
    [ProducesResponseType<SaleLookupResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.GetByIdAsync(CurrentCompanyId, id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<SaleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateSaleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    [HttpPost("{id}/approve")]
    [ProducesResponseType<SaleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.ApproveAsync(CurrentCompanyId, id, cancellationToken));

    /// <summary>One-step POS sale: creates the document already approved and decreases stock in one transaction.</summary>
    [HttpPost("pos-checkout")]
    [ProducesResponseType<SaleResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PosCheckout([FromBody] CreateSaleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.PosCheckoutAsync(CurrentCompanyId, request, cancellationToken));
}
