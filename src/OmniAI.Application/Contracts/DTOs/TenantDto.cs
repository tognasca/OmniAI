using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record TenantDto(Guid Id, string Name, string Slug, TenantPlan Plan, bool IsActive);