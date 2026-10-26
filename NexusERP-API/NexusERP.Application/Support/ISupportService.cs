using NexusERP.Application.Common;

namespace NexusERP.Application.Support;

public interface ISupportService
{
    Task<Result<SupportTicketResponseDto>> CreateTicketAsync(string companyId, string userId, SupportMessageRequestDto request, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> AddUserMessageAsync(string companyId, string userId, string ticketId, SupportMessageRequestDto request, CancellationToken cancellationToken);
    Task<Result<ItemsResponseDto<SupportTicketDto>>> GetMyTicketsAsync(string companyId, string userId, CancellationToken cancellationToken);

    Task<Result<ItemsResponseDto<SupportTicketSummaryDto>>> GetAdminTicketsAsync(string? status, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> GetAdminTicketAsync(string ticketId, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> ReplyAsync(string adminUserId, string ticketId, SupportMessageRequestDto request, CancellationToken cancellationToken);
    Task<Result<SupportTicketResponseDto>> ResolveAsync(string ticketId, CancellationToken cancellationToken);
}
