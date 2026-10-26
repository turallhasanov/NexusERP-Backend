namespace NexusERP.Application.Support;

/// <summary>
/// Pushes a changed ticket to whoever has it open: its owner and the platform team. The API implements it with
/// SignalR; a failed push never fails the change, which is already saved.
/// </summary>
public interface ISupportNotifier
{
    Task TicketChangedAsync(SupportTicketDto ticket, CancellationToken cancellationToken);
}
