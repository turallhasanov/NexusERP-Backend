using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Units;

namespace NexusERP.Api.Controllers;

[Route("api/units")]
[Authorize, RequireCompany]
public class UnitsController(IUnitService unitService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await unitService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUnitRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await unitService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
