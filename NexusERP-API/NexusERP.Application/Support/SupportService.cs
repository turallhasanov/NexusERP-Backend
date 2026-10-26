using Microsoft.EntityFrameworkCore;
using NexusERP.Application.Common;
using NexusERP.Application.Common.Interfaces;
using NexusERP.Domain.Entities;
using NexusERP.Domain.Enums;

namespace NexusERP.Application.Support;

public class SupportService(IApplicationDbContext db, ISupportNotifier notifier) : ISupportService
{
    // Must match SupportMessageConfiguration.
    private const int MessageMaxLength = 2000;

    // A user sees their newest tickets in the widget; older ones stay in the database.
    private const int MyTicketsLimit = 20;

    private const string BotName = "NexusERP Bot";
    private const string SupportTeamName = "Dəstək komandası";

    private const string GreetingText =
        "Salam! 👋 NexusERP Dəstək Xidmətinə xoş gəlmisiniz. Sorğunuz qeydə alındı! 🚀 Mütəxəssislərimiz ən qısa zamanda sizə buradan cavab verəcəkdir.";

    private const string ResolvedText =
        "Bu dəstək sorğusu uğurla həll olundu kimi işarələndi. ✅ Başqa bir sualınız olarsa, istənilən vaxt yeni çat başlada bilərsiniz. Uğurlar!";

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

        // Just after the user's message, so it sorts after it. Not later: a reply or "Söhbəti təmizlə" right away must
        // still come after the greeting.
        ticket.Messages.Add(BotMessage(ticket.Id, GreetingText, now.AddMilliseconds(1)));

        db.SupportTickets.Add(ticket);
        await db.SaveChangesAsync(cancellationToken);

        return await LoadAndPushAsync(ticket.Id, userId, cancellationToken);
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

        return await LoadAndPushAsync(ticket.Id, userId, cancellationToken);
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

        var tickets = await LoadManyAsync(ids, userId, cancellationToken);
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
                t.Messages.Count,
                t.Messages.Count(m => m.SenderType == SupportSenderType.User && !m.IsRead)))
            .ToListAsync(cancellationToken);

        return new ItemsResponseDto<SupportTicketSummaryDto>(items);
    }

    public Task<Result<SupportTicketResponseDto>> GetAdminTicketAsync(string adminUserId, string ticketId, CancellationToken cancellationToken) =>
        LoadAsync(ticketId, adminUserId, cancellationToken);

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

        return await LoadAndPushAsync(ticket.Id, adminUserId, cancellationToken);
    }

    /// <summary>Marks the ticket resolved and leaves a bot note in the conversation. Resolving twice changes nothing.</summary>
    public async Task<Result<SupportTicketResponseDto>> ResolveAsync(string adminUserId, string ticketId, CancellationToken cancellationToken)
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

            return await LoadAndPushAsync(ticket.Id, adminUserId, cancellationToken);
        }

        return await LoadAsync(ticket.Id, adminUserId, cancellationToken);
    }

    /// <summary>The ticket's owner has seen the team's and the bot's messages.</summary>
    public async Task<Result<SupportTicketResponseDto>> MarkReadByUserAsync(string companyId, string userId, string ticketId, CancellationToken cancellationToken)
    {
        if (!await db.SupportTickets.AnyAsync(t => t.Id == ticketId && t.CompanyId == companyId && t.UserId == userId, cancellationToken))
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        return await MarkReadAsync(ticketId, userId, m => m.SenderType != SupportSenderType.User, cancellationToken);
    }

    /// <summary>The platform team has seen the user's messages.</summary>
    public async Task<Result<SupportTicketResponseDto>> MarkReadByTeamAsync(string adminUserId, string ticketId, CancellationToken cancellationToken)
    {
        if (!await db.SupportTickets.AnyAsync(t => t.Id == ticketId, cancellationToken))
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        return await MarkReadAsync(ticketId, adminUserId, m => m.SenderType == SupportSenderType.User, cancellationToken);
    }

    public async Task<Result<SupportTicketResponseDto>> ClearForUserAsync(string companyId, string userId, string ticketId, CancellationToken cancellationToken)
    {
        if (!await db.SupportTickets.AnyAsync(t => t.Id == ticketId && t.CompanyId == companyId && t.UserId == userId, cancellationToken))
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        return await ClearViewAsync(ticketId, userId, cancellationToken);
    }

    public async Task<Result<SupportTicketResponseDto>> ClearForTeamAsync(string adminUserId, string ticketId, CancellationToken cancellationToken)
    {
        if (!await db.SupportTickets.AnyAsync(t => t.Id == ticketId, cancellationToken))
            return Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound);

        return await ClearViewAsync(ticketId, adminUserId, cancellationToken);
    }

    // Hides everything sent so far for this viewer. No push: nobody else's view changes.
    private async Task<Result<SupportTicketResponseDto>> ClearViewAsync(string ticketId, string viewerId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var view = await db.SupportTicketViews.FirstOrDefaultAsync(v => v.TicketId == ticketId && v.UserId == viewerId, cancellationToken);

        if (view is null)
            db.SupportTicketViews.Add(new SupportTicketView { TicketId = ticketId, UserId = viewerId, ClearedAt = now });
        else
            view.ClearedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        return await LoadAsync(ticketId, viewerId, cancellationToken);
    }

    // Marks the unread messages of one side; the other side is told only when something changed.
    private async Task<Result<SupportTicketResponseDto>> MarkReadAsync(
        string ticketId, string viewerId, System.Linq.Expressions.Expression<Func<SupportMessage, bool>> fromOtherSide, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var marked = await db.SupportMessages
            .Where(m => m.TicketId == ticketId && !m.IsRead)
            .Where(fromOtherSide)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true).SetProperty(m => m.ReadAt, now), cancellationToken);

        return marked > 0
            ? await LoadAndPushAsync(ticketId, viewerId, cancellationToken)
            : await LoadAsync(ticketId, viewerId, cancellationToken);
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

    // After a change: the ticket for the caller, pushed to its owner and the team as well. The push leaves out the
    // caller's own clearedAt, which means nothing to the other recipients.
    private async Task<Result<SupportTicketResponseDto>> LoadAndPushAsync(string ticketId, string viewerId, CancellationToken cancellationToken)
    {
        var result = await LoadAsync(ticketId, viewerId, cancellationToken);

        if (result.IsSuccess)
            await notifier.TicketChangedAsync(result.Value!.Ticket with { ClearedAt = null }, cancellationToken);

        return result;
    }

    private async Task<Result<SupportTicketResponseDto>> LoadAsync(string ticketId, string viewerId, CancellationToken cancellationToken)
    {
        var tickets = await LoadManyAsync([ticketId], viewerId, cancellationToken);

        return tickets.Count == 0
            ? Result<SupportTicketResponseDto>.Failure(ErrorType.NotFound, TicketNotFound)
            : new SupportTicketResponseDto(tickets[0]);
    }

    /// <summary>Tickets with their messages, in the order of <paramref name="ids"/>, with <paramref name="viewerId"/>'s clearedAt.</summary>
    private async Task<List<SupportTicketDto>> LoadManyAsync(IReadOnlyCollection<string> ids, string viewerId, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
            return [];

        var tickets = await db.SupportTickets
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Select(t => new SupportTicketDto(
                t.Id,
                t.Status,
                t.UserId,
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
                        m.SentAt,
                        m.IsRead,
                        m.ReadAt))
                    .ToList(),
                db.SupportTicketViews
                    .Where(v => v.TicketId == t.Id && v.UserId == viewerId)
                    .Select(v => (DateTime?)v.ClearedAt)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        var order = ids.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
        return tickets.OrderBy(t => order[t.Id]).ToList();
    }
}
