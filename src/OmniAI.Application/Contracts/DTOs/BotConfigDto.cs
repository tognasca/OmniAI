using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record BotConfigDto(
    Guid Id, Guid WhatsAppNumberId, string SystemPrompt, string AiModel,
    float Temperature, int MaxTokens, bool IsActive,
    TimeOnly? WorkStart, TimeOnly? WorkEnd, string? OutOfHoursMsg);
