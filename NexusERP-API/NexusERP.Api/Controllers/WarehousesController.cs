using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Warehouses;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class WarehousesController(IWarehouseService warehouseService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<WarehouseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.GetActiveAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<WarehouseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
