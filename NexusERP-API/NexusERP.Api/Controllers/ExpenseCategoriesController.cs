using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Expenses;

namespace NexusERP.Api.Controllers;

[Route("api/expense-categories")]
[Authorize, RequireCompany]
public class ExpenseCategoriesController(IExpenseCategoryService expenseCategoryService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await expenseCategoryService.GetActiveAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseCategoryRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await expenseCategoryService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
