using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Stores;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class StoresController(IStoreService storeService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<StoreDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await storeService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<StoreResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await storeService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>Changes name, address and supplying warehouse. Admins and IT admins only; staff get 403.</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<StoreResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateStoreRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await storeService.UpdateAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>
    /// Deletes a store with no sales, expenses or employees still working there (409 otherwise). Admins and IT admins
    /// only; staff get 403.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<SuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await storeService.DeleteAsync(CurrentCompanyId, id, cancellationToken));
}
