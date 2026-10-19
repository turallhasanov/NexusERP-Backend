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
}
