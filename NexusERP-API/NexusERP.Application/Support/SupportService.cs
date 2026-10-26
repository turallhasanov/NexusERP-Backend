using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Support;

public class SupportService(IApplicationDbContext db) : ISupportService
{
    // Must match SupportMessageConfiguration.
    private const int MessageMaxLength = 2000;

    // A user sees their newest tickets in the widget; older ones stay in the database.
    private const int MyTicketsLimit = 20;

    private const string BotName = "NexusERP Bot";
    private const string SupportTeamName = "Dəstək komandası";

    private const string GreetingText = "Salam! Sorğunuz qəbul olundu. Komandamız ən qısa zamanda sualınızı cavablandıracaq.";
    private const string ResolvedText = "Sorğunuz həll olunmuş kimi qeyd edildi. Yeni sualınız olarsa, buraya yazın — yeni müraciət açılacaq.";

    private const string TicketNotFound = "Müraciət tapılmadı.";

    /// <summary>
    /// Opens a ticket with the user's first message and the bot's greeting, saved together, so the reply is already
    /// in the response the user's widget receives.
    /// </summary>
    public async Task<Result<SupportTicketResponseDto>> CreateTicketAsync(string companyId, string userId, SupportMessageRequestDto request, CancellationToken cancellationToken)
    {
        if (ValidateMessage(request.Message) is { } error)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.Validation, error);

        var now = DateTime.UtcNow;
        var ticket = new SupportTicket { CompanyId = companyId, UserId = userId, CreatedAt = now, LastMessageAt = now };

        ticket.Messages.Add(new SupportMessage
        {
            TicketId = ticket.Id,
            SenderUserId = userId,
            SenderType = SupportSenderType.User,
            MessageText = request.Message!.Trim(),
            SentAt = now
        });

        // A moment after the user's message, so it sorts after it.
        ticket.Messages.Add(BotMessage(ticket.Id, GreetingText, now.AddMilliseconds(500)));

        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(ticket.Id, cancellationToken);
    }

    /// <summary>A follow-up from the ticket's owner; the ticket goes back to the team (Open).</summary>
    public async Task<Result<SupportTicketResponseDto>> AddUserMessageAsync(
        string companyId, string userId, string ticketId, SupportMessageRequestDto request, CancellationToken cancellationToken)
    {
        if (ValidateMessage(request.Message) is { } error)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.Validation, error);

        // Another user's ticket looks exactly like a missing one.
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(
            t => t.Id == ticketId && t.CompanyId == companyId && t.UserId == userId, cancellationToken);

        if (ticket is null)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        if (ticket.Status == SupportTicketStatus.Resolved)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.Conflict, "Bu müraciət həll olunub. Yeni sual üçün yeni müraciət açın.");

        var now = DateTime.UtcNow;
        db.SupportMessages.Add(new SupportMessage
        {
            TicketId = ticket.Id,
            SenderUserId = userId,
            SenderType = SupportSenderType.User,
            MessageText = request.Message!.Trim(),
            SentAt = now
        });

        ticket.Status = SupportTicketStatus.Open;
        ticket.LastMessageAt = now;
        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(ticket.Id, cancellationToken);
    }

    public async Task<Result<ItemsResponseDto<SupportTicketDto>>> GetMyTicketsAsync(string companyId, string userId, CancellationToken cancellationToken)
    {
        var ids = await db.SupportTickets
            .AsNoTracking()
            .Where(t => t.CompanyId == companyId && t.UserId == userId)
            .OrderByDescending(t => t.LastMessageAt)
            .Take(MyTicketsLimit)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var tickets = await LoadManyAsync(ids, cancellationToken);
        return new ItemsResponseDto<SupportTicketDto>(tickets);
    }

    /// <summary>
    /// <paramref name="status"/>: "active" (default: open and pending), "open", "pending", "resolved" or "all".
    /// Tickets waiting for the team come first, then by newest activity.
    /// </summary>
    public async Task<Result<ItemsResponseDto<SupportTicketSummaryDto>>> GetAdminTicketsAsync(string? status, CancellationToken cancellationToken)
    {
        var filter = string.IsNullOrWhiteSpace(status) ? "active" : status.Trim().ToLowerInvariant();
        var tickets = db.SupportTickets.AsNoTracking();

        IQueryable<SupportTicket>? query = filter switch
        {
            "active" => tickets.Where(t => t.Status != SupportTicketStatus.Resolved),
            "open" => tickets.Where(t => t.Status == SupportTicketStatus.Open),
            "pending" => tickets.Where(t => t.Status == SupportTicketStatus.Pending),
            "resolved" => tickets.Where(t => t.Status == SupportTicketStatus.Resolved),
            "all" => tickets,
            _ => null
        };

        if (query is null)
            return Result<ItemsResponseDto<SupportTicketSummaryDto>>.Failure(
                ErrorType.Validation, "Status yanlışdır. Mümkün dəyərlər: active, open, pending, resolved, all.");

        var items = await query
            .OrderBy(t => t.Status == SupportTicketStatus.Open ? 0 : 1)
            .ThenByDescending(t => t.LastMessageAt)
            .Select(t => new SupportTicketSummaryDto(
                t.Id,
                t.Status,
                t.CompanyId,
                db.Companies.Where(c => c.Id == t.CompanyId).Select(c => c.Name).First(),
                t.User!.Name,
                t.User.Email,
                t.CreatedAt,
                t.LastMessageAt,
                t.ResolvedAt,
                t.Messages.OrderByDescending(m => m.SentAt).Select(m => m.MessageText).First(),
                t.Messages.OrderByDescending(m => m.SentAt).Select(m => m.SenderType).First(),
                t.Messages.Count))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<SupportTicketSummaryDto>(items);
    }

    public Task<Result<SupportTicketResponseDto>> GetAdminTicketAsync(string ticketId, CancellationToken cancellationToken) =>
        LoadAsync(ticketId, cancellationToken);

    /// <summary>A platform admin's answer; the ticket waits for the user (Pending). A resolved ticket is reopened.</summary>
    public async Task<Result<SupportTicketResponseDto>> ReplyAsync(string adminUserId, string ticketId, SupportMessageRequestDto request, CancellationToken cancellationToken)
    {
        if (ValidateMessage(request.Message) is { } error)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.Validation, error);

        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket is null)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        var now = DateTime.UtcNow;
        db.SupportMessages.Add(new SupportMessage
        {
            TicketId = ticket.Id,
            SenderUserId = adminUserId,
            SenderType = SupportSenderType.SuperAdmin,
            MessageText = request.Message!.Trim(),
            SentAt = now
        });

        ticket.Status = SupportTicketStatus.Pending;
        ticket.LastMessageAt = now;
        ticket.ResolvedAt = null;
        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(ticket.Id, cancellationToken);
    }

    /// <summary>Marks the ticket resolved and leaves a bot note in the conversation. Resolving twice changes nothing.</summary>
    public async Task<Result<SupportTicketResponseDto>> ResolveAsync(string ticketId, CancellationToken cancellationToken)
    {
        var ticket = await db.SupportTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);

        if (ticket is null)
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        if (ticket.Status != SupportTicketStatus.Resolved)
        {
            var now = DateTime.UtcNow;
            db.SupportMessages.Add(BotMessage(ticket.Id, ResolvedText, now));

            ticket.Status = SupportTicketStatus.Resolved;
            ticket.ResolvedAt = now;
            ticket.LastMessageAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await LoadAsync(ticket.Id, cancellationToken);
    }

    private static SupportMessage BotMessage(string ticketId, string text, DateTime sentAt) => new()
    {
        TicketId = ticketId,
        SenderUserId = null,
        SenderType = SupportSenderType.SystemBot,
        MessageText = text,
        SentAt = sentAt
    };

    private static string? ValidateMessage(string? message)
    {
        var text = message?.Trim();

        if (string.IsNullOrEmpty(text))
            return "Mesaj boş ola bilməz.";

        return text.Length > MessageMaxLength ? $"Mesaj {MessageMaxLength} simvoldan uzun ola bilməz." : null;
    }

    private async Task<Result<SupportTicketResponseDto>> LoadAsync(string ticketId, CancellationToken cancellationToken)
    {
        var tickets = await LoadManyAsync([ticketId], cancellationToken);

        return tickets.Count == 0
            ? Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound)
            : new SupportTicketResponseDto(tickets[0]);
    }

    /// <summary>Tickets with their messages, in the order of <paramref name="ids"/>.</summary>
    private async Task<List<SupportTicketDto>> LoadManyAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        var tickets = await db.SupportTickets
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new SupportTicketDto(
                t.Id,
                t.Status,
                t.CompanyId,
                db.Companies.Where(c => c.Id == t.CompanyId).Select(c => c.Name).First(),
                t.User!.Name,
                t.User.Email,
                t.CreatedAt,
                t.LastMessageAt,
                t.ResolvedAt,
                t.Messages
                    .OrderBy(m => m.SentAt)
                    .Select(m => new SupportMessageDto(
                        m.Id,
                        m.SenderType,
                        m.SenderType == SupportSenderType.SystemBot ? BotName
                            : m.SenderType == SupportSenderType.SuperAdmin ? SupportTeamName
                            : m.SenderUser!.Name,
                        m.MessageText,
                        m.SentAt))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return tickets.OrderBy(t => order[t.Id]).ToList();
    }
}
