using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Purchases;

namespace NexusERP.Api.Controllers;

[Route("api/purchases")]
[Authorize, RequireCompany]
public class PurchasesController(IPurchaseService purchaseService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.GetByIdAsync(CurrentCompanyId, id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken) =>
        ToActionResult(await purchaseService.ApproveAsync(CurrentCompanyId, id, cancellationToken));
}
