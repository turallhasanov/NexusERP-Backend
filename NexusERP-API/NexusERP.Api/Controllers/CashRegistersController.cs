using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.CashRegisters;

namespace NexusERP.Api.Controllers;

[Route("api/cash-registers")]
[Authorize, RequireCompany]
public class CashRegistersController(ICashRegisterService cashRegisterService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCashRegisterRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>All payment transactions, newest first. With <paramref name="cashRegisterId"/>, only that register's (incl. transfers into it).</summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions([FromQuery] string? cashRegisterId, CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.GetTransactionsAsync(CurrentCompanyId, cashRegisterId, cancellationToken));

    [HttpPost("transactions")]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.CreateTransactionAsync(CurrentCompanyId, request, cancellationToken));
}
