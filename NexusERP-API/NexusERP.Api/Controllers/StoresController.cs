using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Stores;

namespace NexusERP.Api.Controllers;

[Route("api/stores")]
[Authorize, RequireCompany]
public class StoresController(IStoreService storeService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await storeService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await storeService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
