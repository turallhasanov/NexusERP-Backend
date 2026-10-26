using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexusERP.Api.Middlewares;
using NexusERP.Application.Common;
using NexusERP.Application.Support;

namespace NexusERP.Api.Controllers;

/// <summary>
/// Support chat. Company users open tickets and write in their own; the platform team ("superadmin") answers and
/// resolves them from the admin portal. A new ticket gets the bot's greeting at once.
/// </summary>
[Authorize]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ErrorResponse>(StatusCodes.Status403Forbidden)]
public class SupportController(ISupportService supportService) : ApiControllerBase
{
    /// <summary>Opens a ticket with the first message; the response already holds the bot's greeting.</summary>
    [HttpPost("tickets")]
    [RequireCompany]
    [ProducesResponseType<SupportTicketResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTicket([FromBody] SupportMessageRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await supportService.CreateTicketAsync(CurrentCompanyId, CurrentUserId, request, cancellationToken));

    /// <summary>A follow-up message in one of the caller's own tickets; 409 once it is resolved.</summary>
    [HttpPost("tickets/{id}/messages")]
    [RequireCompany]
    [ProducesResponseType<SupportTicketResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddMessage(string id, [FromBody] SupportMessageRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await supportService.AddUserMessageAsync(CurrentCompanyId, CurrentUserId, id, request, cancellationToken));

    /// <summary>The caller's newest tickets (up to 20) with their messages.</summary>
    [HttpGet("my-tickets")]
    [RequireCompany]
    [ProducesResponseType<ItemsResponseDto<SupportTicketDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTickets(CancellationToken cancellationToken) =>
        ToActionResult(await supportService.GetMyTicketsAsync(CurrentCompanyId, CurrentUserId, cancellationToken));

    /// <summary>Every company's tickets for the support desk. <paramref name="status"/>: active (default), open, pending, resolved, all.</summary>
    [HttpGet("admin/tickets")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<ItemsResponseDto<SupportTicketSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAdminTickets([FromQuery] string? status, CancellationToken cancellationToken) =>
        ToActionResult(await supportService.GetAdminTicketsAsync(status, cancellationToken));

    [HttpGet("admin/tickets/{id}")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<SupportTicketResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdminTicket(string id, CancellationToken cancellationToken) =>
        ToActionResult(await supportService.GetAdminTicketAsync(id, cancellationToken));

    /// <summary>The support team's answer; the ticket then waits for the user.</summary>
    [HttpPost("tickets/{id}/reply")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<SupportTicketResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reply(string id, [FromBody] SupportMessageRequestDto request, CancellationToken cancellationToken) =>
        ToActionResult(await supportService.ReplyAsync(CurrentUserId, id, request, cancellationToken));

    /// <summary>"Sorğu həll olundu": closes the ticket.</summary>
    [HttpPut("tickets/{id}/resolve")]
    [Authorize(Roles = AppRoles.SuperAdmin)]
    [ProducesResponseType<SupportTicketResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve(string id, CancellationToken cancellationToken) =>
        ToActionResult(await supportService.ResolveAsync(id, cancellationToken));
}
