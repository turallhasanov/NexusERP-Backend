using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Expenses;

namespace NexusERP.Api.Controllers;

[Route("api/expenses")]
[Authorize, RequireCompany]
public class ExpensesController(IExpenseService expenseService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await expenseService.GetAllAsync(CurrentCompanyId, cancellationToken));

    /// <summary>Records the expense and pays it from the cash register in one transaction.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await expenseService.CreateAsync(CurrentCompanyId, request, cancellationToken));
}
