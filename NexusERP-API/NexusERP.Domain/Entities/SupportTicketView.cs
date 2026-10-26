namespace NexusERP.Domain.Entities;

/// <summary>
/// One person's view of a ticket: "Söhbəti təmizlə" hides the messages up to <see cref="ClearedAt"/> for that person
/// only. Nothing is deleted; the ticket and its messages stay as they are for everyone else.
/// </summary>
public class SupportTicketView
{
    public string TicketId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ClearedAt { get; set; }
}
