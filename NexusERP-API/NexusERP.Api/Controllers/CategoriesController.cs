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

    /// <summary>Renames the category. Admins and IT admins only; staff get 403.</summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<CategoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCategoryRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await categoryService.UpdateAsync(CurrentCompanyId, id, request, cancellationToken));

    /// <summary>Deletes a category no product uses (409 otherwise). Admins and IT admins only; staff get 403.</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<SuccessResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken) =>
        ToActionResult(await categoryService.DeleteAsync(CurrentCompanyId, id, cancellationToken));
}
