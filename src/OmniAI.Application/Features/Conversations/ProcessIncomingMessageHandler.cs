using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Contracts.Responses;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;
using OmniAI.Domain.Interfaces;

namespace OmniAI.Application.Features.Conversations;

/// <summary>
/// Core message processing pipeline:
/// 1. Upsert contact
/// 2. Get or create conversation
/// 3. Persist inbound message
/// 4. Check handoff triggers
/// 5. If bot active: build context, call AI with RAG, send reply
/// 6. Log AI usage
/// </summary>
public class ProcessIncomingMessageHandler(
    IOmniDbContext db,
    IWhatsAppService whatsApp,
    IAiService ai,
    IKnowledgeIndexingService rag,
    IHandoffService handoff,
    IDashboardNotifier notifier)
    : IRequestHandler<ProcessIncomingMessageCommand, bool>
{
    public async Task<bool> Handle(ProcessIncomingMessageCommand req, CancellationToken ct)
    {
        // ── 1. Find number by PhoneNumberId ──────────────────────────
        var number = await db.WhatsAppNumbers
            .Include(n => n.Config)
            .Include(n => n.Triggers.Where(t => t.IsActive))
            .FirstOrDefaultAsync(n => n.PhoneNumberId == req.PhoneNumberId, ct);

        if (number is null) return false;

        // ── 2. Upsert contact ─────────────────────────────────────────
        var contact = await db.Contacts
            .FirstOrDefaultAsync(c => c.TenantId == number.TenantId
                                   && c.PhoneNumber == req.From, ct);
        if (contact is null)
        {
            contact = new Contact
            {
                Id = Guid.NewGuid(),
                TenantId = number.TenantId,
                PhoneNumber = req.From,
                FirstSeenAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            db.Contacts.Add(contact);
        }
        else
        {
            contact.LastSeenAt = DateTime.UtcNow;
        }

        // ── 3. Get or create open conversation ────────────────────────
        var conversation = await db.Conversations
            .Where(c => c.ContactId == contact.Id
                     && c.WhatsAppNumberId == number.Id
                     && (c.Status == ConversationStatus.Open
                      || c.Status == ConversationStatus.WaitingHuman
                      || c.Status == ConversationStatus.InProgress))
            .OrderByDescending(c => c.StartedAt)
            .FirstOrDefaultAsync(ct);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                ContactId = contact.Id,
                WhatsAppNumberId = number.Id,
                Status = ConversationStatus.Open,
                StartedAt = DateTime.UtcNow,
            };
            db.Conversations.Add(conversation);
        }

        // ── 4. Persist inbound message (idempotent via WaMessageId) ──
        var exists = await db.Messages.AnyAsync(m => m.WaMessageId == req.WaMessageId, ct);
        if (exists) return true; // already processed

        var inbound = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            WaMessageId = req.WaMessageId,
            Direction = MessageDirection.Inbound,
            Type = Enum.TryParse<MessageType>(req.MessageType, true, out var mt) ? mt : MessageType.Text,
            SenderType = SenderType.User,
            Content = req.Content,
            SentAt = DateTime.UtcNow,
        };
        db.Messages.Add(inbound);
        if (string.Equals(req.MessageType, "text", StringComparison.OrdinalIgnoreCase) &&
            req.Content?.Trim().ToUpperInvariant() is ("SAIR" or "STOP" or "DESCADASTRAR"))
            contact.IsBlocked = true;
        await db.SaveChangesAsync(ct);
        if (contact.IsBlocked)
            return true;

        await notifier.NotifyNewMessageAsync(conversation.Id, req.Content ?? "", SenderType.User);

        // ── 5. Skip AI if conversation is waiting/in-progress with human
        if (conversation.Status is ConversationStatus.WaitingHuman
                                 or ConversationStatus.InProgress)
            return true;

        // ── 6. Check handoff triggers ─────────────────────────────────
        if (req.Content is not null)
        {
            var shouldHandoff = await handoff.CheckTriggersAsync(number.Id, req.Content, ct);
            if (shouldHandoff)
            {
                await handoff.TriggerHandoffAsync(conversation.Id, "keyword", ct);
                await notifier.NotifyHandoffRequestedAsync(
                    conversation.Id, contact.Name ?? req.From, req.Content);
                return true;
            }
        }

        // ── 7. Check business hours ───────────────────────────────────
        var config = number.Config;
        if (config is { IsActive: false })
            return true;
        if (config is { IsActive: true })
        {
            var now = TimeOnly.FromDateTime(DateTime.Now);
            if (config.WorkStart.HasValue && config.WorkEnd.HasValue)
            {
                if (now < config.WorkStart.Value || now > config.WorkEnd.Value)
                {
                    var outMsg = config.OutOfHoursMsg
                        ?? "Olá! Nosso atendimento está fora do horário. Retornaremos em breve. 🕐";
                    await SendReplyAsync(number, req.From, conversation.Id, outMsg,
                        SenderType.Bot, ct);
                    return true;
                }
            }
        }

        // ── 8. Build AI context with RAG ──────────────────────────────
        var ragChunks = req.Content is not null
            ? (await rag.SearchAsync(number.Id, req.Content, topK: 5, ct)).ToList()
            : [];

        var history = await db.Messages
            .Where(m => m.ConversationId == conversation.Id && m.Id != inbound.Id)
            .OrderByDescending(m => m.SentAt)
            .Take(config?.ContextMessages ?? 10)
            .OrderBy(m => m.SentAt)
            .Select(m => new { m.SenderType, m.Content })
            .ToListAsync(ct);

        var systemPrompt = config?.SystemPrompt
            ?? "Você é um assistente virtual prestativo. Responda sempre em português brasileiro.";

        if (ragChunks.Count > 0)
        {
            var context = string.Join("\n\n", ragChunks);
            systemPrompt += $"\n\n## Contexto relevante da base de conhecimento:\n{context}";
        }

        var historyTuples = history
            .Select(h => (h.SenderType == SenderType.User ? "user" : "assistant",
                          h.Content ?? ""))
            .ToList();

        // ── 9. Call AI ────────────────────────────────────────────────
        var completion = await ai.CompleteAsync(new AiCompletionRequest(
            systemPrompt, historyTuples, req.Content ?? "", config?.AiModel ?? "",
            config?.MaxTokens ?? 1000, config?.Temperature ?? 0.3f), ct);
        var aiReply = completion.Text;
        if (string.IsNullOrWhiteSpace(aiReply))
            return true;

        // Check if AI itself suggests handoff
        var aiSuggestsHandoff = await ai.ShouldHandoffAsync(aiReply,
            number.Triggers.Select(t => t.Value), ct);

        if (aiSuggestsHandoff)
        {
            await handoff.TriggerHandoffAsync(conversation.Id, "ai-intent", ct);
            await notifier.NotifyHandoffRequestedAsync(
                conversation.Id, contact.Name ?? req.From, req.Content ?? "");
            return true;
        }

        // ── 10. Send reply and log usage ──────────────────────────────
        await SendReplyAsync(number, req.From, conversation.Id, aiReply,
            SenderType.Bot, ct, completion, ragChunks.Count);

        return true;
    }

    private async Task SendReplyAsync(
        WhatsAppNumber number, string to, Guid conversationId,
        string text, SenderType sender, CancellationToken ct, AiCompletion? completion = null, int ragCount = 0)
    {
        await whatsApp.SendTextAsync(number.Id, to, text, ct);

        var outbound = new Message
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Direction = MessageDirection.Outbound,
            Type = MessageType.Text,
            SenderType = sender,
            Content = text,
            SentAt = DateTime.UtcNow,
        };
        db.Messages.Add(outbound);
        if (completion is not null)
        {
            outbound.PromptTokens = completion.InputTokens;
            outbound.CompletionTokens = completion.OutputTokens;
            db.AiUsageLogs.Add(new AiUsageLog
            {
                Id = Guid.NewGuid(), MessageId = outbound.Id, WhatsAppNumberId = number.Id,
                Model = completion.Model, PromptTokens = completion.InputTokens,
                CompletionTokens = completion.OutputTokens,
                TotalTokens = completion.InputTokens + completion.OutputTokens,
                RagUsed = ragCount > 0, RagChunksUsed = ragCount, CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
        await notifier.NotifyNewMessageAsync(conversationId, text, sender);
    }


    // ── GET messages ──────────────────────────────────────────────────
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
                .Select(m => new MessageDto(
                    m.Id, m.ConversationId, m.WaMessageId,
                    m.Direction, m.Type, m.SenderType,
                    m.Content, m.MediaUrl,
                    m.SentAt, m.DeliveredAt, m.ReadAt))
                .ToListAsync(ct);

            return new PagedResult<MessageDto>(items, total, req.Page, req.PageSize);
        }
    }
}
