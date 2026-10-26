using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NexusERP.Application.Common;

namespace NexusERP.Api.Hubs;

/// <summary>
/// Live support chat at /hubs/support: the server pushes "ticketUpdated" (a whole ticket with its messages) to the
/// ticket's owner and to every platform admin. Clients only listen; they write through the REST endpoints, which keep
/// validation, permissions and the read-only rule for impersonation in one place.
/// </summary>
[Authorize]
public class SupportChatHub : Hub
{
    public const string Path = "/hubs/support";
    public const string TeamGroup = "support-team";
    public const string TicketUpdated = "ticketUpdated";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.IsInRole(AppRoles.SuperAdmin) == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, TeamGroup);

        await base.OnConnectedAsync();
    }
}

/// <summary>SignalR's user id is the JWT "sub" claim, so Clients.User(userId) reaches every tab of that account.</summary>
public sealed class SubClaimUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User?.FindFirst(AppClaimTypes.UserId)?.Value;
}

public sealed class SignalRSupportNotifier(IHubContext<SupportChatHub> hub, ILogger<SignalRSupportNotifier> logger)
    : NexusERP.Application.Support.ISupportNotifier
{
    public async Task TicketChangedAsync(NexusERP.Application.Support.SupportTicketDto ticket, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.User(ticket.UserId).SendAsync(SupportChatHub.TicketUpdated, ticket, cancellationToken);
            await hub.Clients.Group(SupportChatHub.TeamGroup).SendAsync(SupportChatHub.TicketUpdated, ticket, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change is saved; clients catch up when they next load or reconnect.
            logger.LogWarning(exception, "Could not push support ticket {TicketId}", ticket.Id);
        }
    }
}
