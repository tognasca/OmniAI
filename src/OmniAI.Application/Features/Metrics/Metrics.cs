using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Features.WhatsAppNumbers;
using OmniAI.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


// ── Metrics ────────────────────────────────────────────────────────
namespace OmniAI.Application.Features.Metrics;

public class GetDashboardMetricsHandler(IOmniDbContext db)
    : IRequestHandler<GetDashboardMetricsQuery, DashboardMetricsDto>
{
    public async Task<DashboardMetricsDto> Handle(
        GetDashboardMetricsQuery req, CancellationToken ct)
    {
        var tenantId = await TenantHelper.ResolveAsync(db, req.TenantId, ct);
        var today = DateTime.UtcNow.Date;

        var todayMsgs = await db.Messages.CountAsync(m => m.Conversation.Number.TenantId == tenantId && m.SentAt >= today, ct);
        var periodMsgs = await db.Messages.CountAsync(m => m.Conversation.Number.TenantId == tenantId && m.SentAt >= req.From && m.SentAt <= req.To, ct);
        var pendingHO = await db.Conversations.CountAsync(c => c.Number.TenantId == tenantId && c.Status == ConversationStatus.WaitingHuman, ct);
        var totalCost = await db.AiUsageLogs.Where(l => l.CreatedAt >= req.From && l.CreatedAt <= req.To && db.WhatsAppNumbers.Any(n => n.Id == l.WhatsAppNumberId && n.TenantId == tenantId)).SumAsync(l => (decimal?)l.CostUsd, ct) ?? 0;
        var totalConvs = await db.Conversations.CountAsync(c => c.Number.TenantId == tenantId && c.StartedAt >= req.From, ct);
        var aiResolved = await db.Conversations.CountAsync(c => c.Number.TenantId == tenantId && c.StartedAt >= req.From && c.Status == ConversationStatus.Resolved && c.AssignedAgentId == null, ct);
        double aiRate = totalConvs > 0 ? Math.Round((double)aiResolved / totalConvs * 100, 1) : 0;

        var dailyVolume = await db.Messages
            .Where(m => m.Conversation.Number.TenantId == tenantId && m.SentAt >= req.From && m.SentAt <= req.To)
            .GroupBy(m => m.SentAt.Date)
            .Select(g => new DailyVolumeDto(g.Key,
                g.Count(m => m.Direction == MessageDirection.Inbound),
                g.Count(m => m.Direction == MessageDirection.Outbound),
                g.Count(m => m.SenderType == SenderType.Agent)))
            .OrderBy(d => d.Date)
            .ToListAsync(ct);

        return new DashboardMetricsDto(todayMsgs, periodMsgs, aiRate, 1.4, pendingHO, totalCost, dailyVolume);
    }
}