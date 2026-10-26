using NexusERP.Application.Common;

namespace NexusERP.Application.Support;

public interface ISupportService
{
    Task<Result<SupportTicketResponseDto>> CreateTicketAsync(string companyId, string userId, SupportMessageRequestDto request, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> AddUserMessageAsync(string companyId, string userId, string ticketId, SupportMessageRequestDto request, CancellationToken cancellationToken);
    Task<Result<ItemsResponseDto<SupportTicketDto>>> GetMyTicketsAsync(string companyId, string userId, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> MarkReadByUserAsync(string companyId, string userId, string ticketId, CancellationToken cancellationToken);

    Task<Result<ItemsResponseDto<SupportTicketSummaryDto>>> GetAdminTicketsAsync(string? status, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> GetAdminTicketAsync(string adminUserId, string ticketId, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> ReplyAsync(string adminUserId, string ticketId, SupportMessageRequestDto request, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> ResolveAsync(string adminUserId, string ticketId, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> MarkReadByTeamAsync(string adminUserId, string ticketId, CancellationToken cancellationToken);

    /// <summary>"Söhbəti təmizlə" for the ticket's owner: hides the conversation so far, for them only.</summary>
    Task<Result<SupportTicketResponseDto>> ClearForUserAsync(string companyId, string userId, string ticketId, CancellationToken cancellationToken);

    /// <summary>"Söhbəti təmizlə" on the desk: hides the conversation so far for this platform admin only.</summary>
    Task<Result<SupportTicketResponseDto>> ClearForTeamAsync(string adminUserId, string ticketId, CancellationToken cancellationToken);
}
