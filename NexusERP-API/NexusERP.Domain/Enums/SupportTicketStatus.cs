namespace NexusERP.Domain.Enums;

public enum SupportTicketStatus
{
    /// <summary>Waiting for the support team: new, or the user wrote again.</summary>
    Open = 1,

    /// <summary>The support team answered; waiting for the user.</summary>
    Pending = 2,

    /// <summary>Closed by the support team; a new message from the user starts a new ticket.</summary>
    Resolved = 3
}
