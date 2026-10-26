using NexusERP.Domain.Enums;

namespace NexusERP.Domain.Entities;

/// <summary>A support conversation a company user started with the platform team.</summary>
public class SupportTicket
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CompanyId { get; set; } = string.Empty;

    /// <summary>The user who opened the ticket; only they see it on the company side.</summary>
    public string UserId { get; set; } = string.Empty;

    public SupportTicketStatus Status { get; set; } = SupportTicketStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Time of the newest message, for sorting the support desk.</summary>
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }

    public User? User { get; set; }
    public ICollection<SupportMessage> Messages { get; set; } = new List<SupportMessage>();
}
