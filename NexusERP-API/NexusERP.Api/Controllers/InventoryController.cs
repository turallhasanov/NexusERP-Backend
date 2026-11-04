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

    /// <summary>
    /// Manual stock receipt: adds the quantity to the product's balance in the warehouse. When the product is shown on
    /// the Khazar store, its new total is sent there (a failed push is logged and does not undo the receipt).
    /// </summary>
    [HttpPost("receive")]
    [ProducesResponseType<ReceiveStockResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReceiveStock([FromBody] ReceiveStockRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await inventoryService.ReceiveStockAsync(CurrentCompanyId, request, cancellationToken));
}
