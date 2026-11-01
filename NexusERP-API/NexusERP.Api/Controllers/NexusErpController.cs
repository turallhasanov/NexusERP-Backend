using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Integration;

namespace NexusERP.Api.Controllers;

[Authorize(Roles = AppRoles.CompanyManagers), RequireCompany]
// The default api/[controller] route is kebab-cased ("nexus-erp"); the frontend calls /api/nexuserp.
[Route("api/nexuserp")]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class NexusErpController(ICatalogSyncService catalogSyncService) : ApiControllerBase
{
    /// <summary>
    /// Data integration: reads the Khazar catalog and upserts every active variant as a product (barcode = SKU) with
    /// Khazar's price and stock. Admins and IT admins only.
    /// </summary>
    [HttpPost("reset-stock")]
    [ProducesResponseType<ResetStockResponseDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetStock(CancellationToken cancellationToken) =>
        ToActionResult(await catalogSyncService.ResetStockAsync(CurrentCompanyId, cancellationToken));

    [HttpPost("sync-all-data")]
    [ProducesResponseType<CatalogSyncResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SyncAllData(CancellationToken cancellationToken) =>
        ToActionResult(await catalogSyncService.SyncAllAsync(CurrentCompanyId, cancellationToken));
}
