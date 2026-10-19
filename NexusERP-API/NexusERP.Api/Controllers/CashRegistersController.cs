using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.CashRegisters;
using NexusERP.Application.Common;

namespace NexusERP.Api.Controllers;

[Authorize, RequireCompany]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class CashRegistersController(ICashRegisterService cashRegisterService) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ItemsResponseDto<CashRegisterDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.GetAllAsync(CurrentCompanyId, cancellationToken));

    [HttpPost]
    [ProducesResponseType<CashRegisterResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCashRegisterRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.CreateAsync(CurrentCompanyId, request, cancellationToken));

    /// <summary>All payment transactions, newest first. With <paramref name="cashRegisterId"/>, only that register's (incl. transfers into it).</summary>
    [HttpGet("transactions")]
    [ProducesResponseType<ItemsResponseDto<TransactionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactions([FromQuery] string? cashRegisterId, CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.GetTransactionsAsync(CurrentCompanyId, cashRegisterId, cancellationToken));

    [HttpPost("transactions")]
    [ProducesResponseType<TransactionResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTransaction([FromBody] CreateTransactionRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await cashRegisterService.CreateTransactionAsync(CurrentCompanyId, request, cancellationToken));
}
