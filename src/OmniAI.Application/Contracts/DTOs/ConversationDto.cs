using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;


public record ConversationDto(
    Guid Id, Guid ContactId, Guid WhatsAppNumberId,
    ConversationStatus Status, HandoffReason HandoffReason,
    string? AgentName, DateTime StartedAt, DateTime? ClosedAt,
    int MessageCount, string? LastMessage);
