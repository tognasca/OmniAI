using OmniAI.Domain.Enums;

namespace OmniAI.Application.Contracts.DTOs;

public record KnowledgeSourceDto(
    Guid Id, Guid WhatsAppNumberId, SourceType SourceType,
    string Title, int TokenCount, SourceStatus Status, DateTime? IndexedAt);
