using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Contracts.Responses;
using OmniAI.Application.Features.WhatsAppNumbers;

// ── Conversations ──────────────────────────────────────────────────
namespace OmniAI.Application.Features.Conversations;

// ✅ Handler adicionado — estava faltando
public class GetConversationsHandler(IOmniDbContext db)
    : IRequestHandler<GetConversationsQuery, PagedResult<ConversationDto>>
{
    public async Task<PagedResult<ConversationDto>> Handle(
        GetConversationsQuery req, CancellationToken ct)
    {
        var tenantId = await TenantHelper.ResolveAsync(db, req.TenantId, ct);

        var query = db.Conversations
            .Where(c => c.Number.TenantId == tenantId);

        if (req.Status.HasValue)
            query = query.Where(c => c.Status == req.Status.Value);

        query = query.OrderByDescending(c => c.StartedAt);

        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(c => new ConversationDto(
                c.Id,
                c.ContactId,
                c.WhatsAppNumberId,
                c.Status,
                c.HandoffReason,
                c.AgentName,
                c.StartedAt,
                c.ClosedAt,
                c.Messages.Count,
                c.Messages.OrderByDescending(m => m.SentAt).Select(m => m.Content).FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<ConversationDto>(items, total, req.Page, req.PageSize);
    }
}

public class GetConversationMessagesHandler(IOmniDbContext db)
    : IRequestHandler<GetConversationMessagesQuery, PagedResult<MessageDto>>
{
    public async Task<PagedResult<MessageDto>> Handle(
        GetConversationMessagesQuery req, CancellationToken ct)
    {
        var query = db.Messages
            .Where(m => m.ConversationId == req.ConversationId)
            .OrderByDescending(m => m.SentAt);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((req.Page - 1) * req.PageSize)
            .Take(req.PageSize)
            .Select(m => new MessageDto(m.Id, m.ConversationId, m.WaMessageId,
                m.Direction, m.Type, m.SenderType, m.Content, m.MediaUrl,
                m.SentAt, m.DeliveredAt, m.ReadAt))
            .ToListAsync(ct);

        return new PagedResult<MessageDto>(items, total, req.Page, req.PageSize);
    }
}