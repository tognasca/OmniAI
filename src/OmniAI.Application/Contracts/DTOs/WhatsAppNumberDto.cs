using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record WhatsAppNumberDto(
    Guid Id, Guid TenantId, string PhoneNumberId, string WabaId,
    string DisplayName, NumberStatus Status, DateTime? ConnectedAt);