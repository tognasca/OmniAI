using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Contracts.Responses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ── Conversations ──────────────────────────────────────────────────
namespace OmniAI.Application.Features.Conversations;

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