using NexusERP.Application.Common;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Support;

/// <summary>Body of POST /api/support/tickets, /tickets/{id}/messages and /tickets/{id}/reply.</summary>
public sealed record SupportMessageRequestDto(string? Message);

/// <summary><c>senderName</c> is the user's name, "NexusERP Bot" or "Dəstək komandası".</summary>
public sealed record SupportMessageDto(string Id, SupportSenderType SenderType, string SenderName, string MessageText, DateTime SentAt);

/// <summary>A ticket with its whole conversation, oldest message first.</summary>
public sealed record SupportTicketDto(
    string Id,
    SupportTicketStatus Status,
    string CompanyId,
    string CompanyName,
    string UserName,
    string UserEmail,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    DateTime? ResolvedAt,
    IReadOnlyList<SupportMessageDto> Messages);

/// <summary>Row of the support desk: the ticket without its messages, with the newest one as a preview.</summary>
public sealed record SupportTicketSummaryDto(
    string Id,
    SupportTicketStatus Status,
    string CompanyId,
    string CompanyName,
    string UserName,
    string UserEmail,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    DateTime? ResolvedAt,
    string LastMessage,
    SupportSenderType LastSenderType,
    int MessageCount);

/// <summary>{ "ok": true, "ticket": { ... } }</summary>
public sealed record SupportTicketResponseDto(SupportTicketDto Ticket) : SuccessResponse;
