using MediatR;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Responses;
using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.Requests;

// ── Conversations ──────────────────────────────────────────────
public record GetConversationsQuery(Guid TenantId, ConversationStatus? Status, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<ConversationDto>>;

public record GetConversationMessagesQuery(Guid ConversationId, int Page = 1, int PageSize = 50)
    : IRequest<PagedResult<MessageDto>>;

public record AssignAgentCommand(Guid ConversationId, Guid AgentId, string AgentName)
    : IRequest<bool>;

public record CloseConversationCommand(Guid ConversationId, int? CsatScore, string? CsatComment)
    : IRequest<bool>;

// ── WhatsApp Numbers ───────────────────────────────────────────
public record GetNumbersQuery(Guid TenantId) : IRequest<IEnumerable<WhatsAppNumberDto>>;

public record AddWhatsAppNumberCommand(
    Guid TenantId, string PhoneNumberId, string WabaId,
    string DisplayName, string AccessToken, string VerifyToken)
    : IRequest<WhatsAppNumberDto>;

public record UpdateNumberStatusCommand(Guid NumberId, NumberStatus Status) : IRequest<bool>;

// ── Bot Config ─────────────────────────────────────────────────
public record GetBotConfigQuery(Guid WhatsAppNumberId) : IRequest<BotConfigDto?>;

public record UpsertBotConfigCommand(
    Guid WhatsAppNumberId, string SystemPrompt, string AiModel,
    float Temperature, int MaxTokens, bool IsActive,
    TimeOnly? WorkStart, TimeOnly? WorkEnd, string? OutOfHoursMsg)
    : IRequest<BotConfigDto>;

// ── Knowledge Base ─────────────────────────────────────────────
public record GetKnowledgeSourcesQuery(Guid WhatsAppNumberId) : IRequest<IEnumerable<KnowledgeSourceDto>>;

public record AddKnowledgeTextCommand(Guid WhatsAppNumberId, string Title, string Content)
    : IRequest<KnowledgeSourceDto>;

public record AddKnowledgeUrlCommand(Guid WhatsAppNumberId, string Title, string Url)
    : IRequest<KnowledgeSourceDto>;

public record AddKnowledgeFileCommand(Guid WhatsAppNumberId, string Title, string FilePath, SourceType SourceType)
    : IRequest<KnowledgeSourceDto>;

public record DeleteKnowledgeSourceCommand(Guid SourceId) : IRequest<bool>;

// ── Handoff Triggers ───────────────────────────────────────────
public record GetHandoffTriggersQuery(Guid WhatsAppNumberId) : IRequest<IEnumerable<HandoffTriggerDto>>;

public record UpsertHandoffTriggerCommand(
    Guid? Id, Guid WhatsAppNumberId, TriggerType TriggerType,
    string Value, TriggerPriority Priority, bool IsActive, string? TransitionMsg)
    : IRequest<HandoffTriggerDto>;

public record DeleteHandoffTriggerCommand(Guid TriggerId) : IRequest<bool>;

// ── Metrics ────────────────────────────────────────────────────
public record GetDashboardMetricsQuery(Guid TenantId, DateTime From, DateTime To)
    : IRequest<DashboardMetricsDto>;

// ── Incoming Webhook ───────────────────────────────────────────
public record ProcessIncomingMessageCommand(
    string PhoneNumberId, string WaMessageId, string From,
    string MessageType, string? Content, string? MediaId)
    : IRequest<bool>;

