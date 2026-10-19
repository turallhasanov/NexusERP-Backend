using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Units;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class UnitsController(IUnitService unitService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<UnitDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await unitService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<UnitResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUnitRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await unitService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
