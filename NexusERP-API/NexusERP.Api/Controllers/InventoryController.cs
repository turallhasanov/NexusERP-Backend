using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Inventory;

namespace NexusERP.Api.Controllers;

[Route("api/inventory")]
[Authorize, RequireCompany]
public class InventoryController(IInventoryService inventoryService) : ApiControllerBase
{
    [HttpGet("stocks")]
    public async Task<IActionResult> GetStocks(CancellationToken cancellationToken) =>
        ToActionResult(await inventoryService.GetStocksAsync(CurrentCompanyId, cancellationToken));
}
