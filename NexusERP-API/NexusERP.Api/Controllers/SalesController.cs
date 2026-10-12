using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Sales;

namespace NexusERP.Api.Controllers;

[Route("api/sales")]
[Authorize, RequireCompany]
public class SalesController(ISaleService saleService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await saleService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.GetByIdAsync(CurrentCompanyId, id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSaleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.ApproveAsync(CurrentCompanyId, id, cancellationToken));

    /// <summary>One-step POS sale: creates the document already approved and decreases stock in one transaction.</summary>
    [HttpPost("pos-checkout")]
    public async Task<IActionResult> PosCheckout([FromBody] CreateSaleRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await saleService.PosCheckoutAsync(CurrentCompanyId, request, cancellationToken));
}
