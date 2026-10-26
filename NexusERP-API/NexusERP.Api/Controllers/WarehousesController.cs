using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Warehouses;

namespace NexusERP.Api.Controllers;

/// <summary>
/// Warehouse management (Anbar İdarəetməsi). Reading stays open to every company user, since purchase, sale,
/// store and POS forms pick warehouses from this list; changes are for admins and IT admins only.
/// </summary>
[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class WarehousesController(IWarehouseService warehouseService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<WarehouseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.GetActiveAsync(CurrentCompanyId, cancellationToken));

    /// <summary>The warehouse with its current stock and newest approved purchases and sales.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType<WarehouseDetailsResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.GetDetailsAsync(CurrentCompanyId, id, cancellationToken));

    /// <summary>Admins and IT admins only; staff get 403.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<WarehouseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>Admins and IT admins only; staff get 403.</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<WarehouseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateWarehouseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.UpdateAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>
    /// 409 while it holds stock, supplies an active store or has draft documents. Admins and IT admins only;
    /// staff get 403.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<SuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await warehouseService.DeleteAsync(CurrentCompanyId, id, cancellationToken));
}
