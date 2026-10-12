using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Categories;

namespace NexusERP.Api.Controllers;

[Route("api/categories")]
[Authorize, RequireCompany]
public class CategoriesController(ICategoryService categoryService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await categoryService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await categoryService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
