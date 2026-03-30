using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record HandoffTriggerDto(
    Guid Id, Guid WhatsAppNumberId, TriggerType TriggerType,
    string Value, TriggerPriority Priority, bool IsActive);
