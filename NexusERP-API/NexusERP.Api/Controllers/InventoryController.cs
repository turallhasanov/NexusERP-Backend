using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Inventory;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class InventoryController(IInventoryService inventoryService) : ApiControllerBase
{
    [HttpGet("stocks")]
    [ProducesResponseType<ItemsResponseDto<StockDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStocks(CancellationToken cancellationToken) =>
        ToActionResult(await inventoryService.GetStocksAsync(CurrentCompanyId, cancellationToken));
}
