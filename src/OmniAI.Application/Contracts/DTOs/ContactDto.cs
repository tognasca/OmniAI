using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record ContactDto(
    Guid Id, Guid TenantId, string PhoneNumber, string? Name,
    bool IsBlocked, DateTime FirstSeenAt, DateTime LastSeenAt);