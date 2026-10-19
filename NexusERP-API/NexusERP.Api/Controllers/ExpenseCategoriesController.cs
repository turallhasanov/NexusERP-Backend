using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Expenses;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class ExpenseCategoriesController(IExpenseCategoryService expenseCategoryService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<ExpenseCategoryDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await expenseCategoryService.GetActiveAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<ExpenseCategoryResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCategoryRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await expenseCategoryService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
