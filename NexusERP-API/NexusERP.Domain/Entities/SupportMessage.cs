using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

public class SupportMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TicketId { get; set; } = string.Empty;

    /// <summary>The user or platform admin who wrote it; null for <see cref="SupportSenderType.SystemBot"/>.</summary>
    public string? SenderUserId { get; set; }

    public SupportSenderType SenderType { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Seen by the other side: the team for a user's message, the user for the team's and the bot's.</summary>
    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public User? SenderUser { get; set; }
}
