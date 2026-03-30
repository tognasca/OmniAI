using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniAI.Application;
using OmniAI.Domain.Enums;
using OmniAI.Domain.Interfaces;

namespace OmniAI.Infrastructure.Services;

public class HandoffService(
    IOmniDbContext db,
    IWhatsAppService whatsApp,
    IDashboardNotifier notifier,
    ILogger<HandoffService> logger) : IHandoffService
{
    public async Task<bool> CheckTriggersAsync(Guid whatsAppNumberId, string message, CancellationToken ct = default)
    {
        var triggers = await db.HandoffTriggers
            .Where(t => t.WhatsAppNumberId == whatsAppNumberId && t.IsActive)
            .OrderByDescending(t => t.Priority)
            .ToListAsync(ct);

        var lower = message.ToLowerInvariant();

        foreach (var trigger in triggers)
        {
            bool matched = trigger.TriggerType switch
            {
                TriggerType.Keyword => lower.Contains(trigger.Value.ToLowerInvariant()),
                TriggerType.Regex   => System.Text.RegularExpressions.Regex.IsMatch(message, trigger.Value),
                _                   => lower.Contains(trigger.Value.ToLowerInvariant())
            };

            if (matched)
            {
                logger.LogInformation("Handoff trigger matched: '{Value}' in message", trigger.Value);
                return true;
            }
        }

        return false;
    }

    public async Task TriggerHandoffAsync(Guid conversationId, string reason, CancellationToken ct = default)
    {
        var conv = await db.Conversations
            .Include(c => c.Contact)
            .Include(c => c.Number)
            .ThenInclude(n => n.Triggers.Where(t => t.IsActive))
            .FirstOrDefaultAsync(c => c.Id == conversationId, ct);

        if (conv is null) return;

        conv.Status = ConversationStatus.WaitingHuman;
        conv.HandoffReason = reason switch
        {
            "keyword"    => HandoffReason.KeywordTrigger,
            "ai-intent"  => HandoffReason.IntentDetected,
            "sla"        => HandoffReason.SlaBreached,
            _            => HandoffReason.AgentRequested
        };

        await db.SaveChangesAsync(ct);

        // Send transition message to user
        var transitionMsg = conv.Number.Triggers
            .FirstOrDefault(t => t.TransitionMsg != null)?.TransitionMsg
            ?? "Estou transferindo você para um especialista. Em breve entraremos em contato! ✅";

        await whatsApp.SendTextAsync(conv.WhatsAppNumberId, conv.Contact.PhoneNumber, transitionMsg, ct);

        // Notify dashboard via SignalR
        await notifier.NotifyHandoffRequestedAsync(
            conversationId,
            conv.Contact.Name ?? conv.Contact.PhoneNumber,
            "Gatilho de transferência ativado");

        logger.LogInformation("Handoff triggered for conversation {Id}. Reason: {Reason}", conversationId, reason);
    }

    public async Task AssignAgentAsync(Guid conversationId, Guid agentId, string agentName, CancellationToken ct = default)
    {
        var conv = await db.Conversations.FindAsync([conversationId], ct);
        if (conv is null) return;

        conv.AssignedAgentId = agentId;
        conv.AgentName = agentName;
        conv.Status = ConversationStatus.InProgress;

        await db.SaveChangesAsync(ct);
        await notifier.NotifyConversationUpdatedAsync(conversationId, ConversationStatus.InProgress);
    }
}
