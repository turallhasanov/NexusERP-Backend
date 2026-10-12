using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Warehouses;

namespace NexusERP.Api.Controllers;

[Route("api/warehouses")]
[Authorize, RequireCompany]
public class WarehousesController(IWarehouseService warehouseService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.GetActiveAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
