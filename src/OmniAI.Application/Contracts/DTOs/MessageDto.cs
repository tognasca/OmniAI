using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;


public record MessageDto(
    Guid Id, Guid ConversationId, string? WaMessageId,
    MessageDirection Direction, MessageType Type, SenderType SenderType,
    string? Content, string? MediaUrl,
    DateTime SentAt, DateTime? DeliveredAt, DateTime? ReadAt);