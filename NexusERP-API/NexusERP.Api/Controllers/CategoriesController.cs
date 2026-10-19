using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Categories;
using NexusERP.Application.Common;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class CategoriesController(ICategoryService categoryService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await categoryService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CategoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await categoryService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
