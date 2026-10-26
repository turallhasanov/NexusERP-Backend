using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Expenses;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class ExpensesController(IExpenseService expenseService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<ExpenseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await expenseService.GetAllAsync(CurrentCompanyId, cancellationToken));

    /// <summary>Records the expense and pays it from the cash register in one transaction.</summary>
    [HttpPost]
    [ProducesResponseType<ExpenseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await expenseService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>Moves the expense to another store, or to none (company-wide). Admins and IT admins only.</summary>
    [HttpPut("{id}/store")]
    [Authorize(Roles = AppRoles.CompanyManagers)]
    [ProducesResponseType<ExpenseResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStore(string id, [FromBody] UpdateExpenseStoreRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await expenseService.UpdateStoreAsync(CurrentCompanyId, id, request, cancellationToken));
}
