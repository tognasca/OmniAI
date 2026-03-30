namespace OmniAI.Domain.Interfaces;

public interface IKnowledgeIndexingService
{
    Task IndexSourceAsync(Guid knowledgeSourceId, CancellationToken ct = default);
    Task<IEnumerable<string>> SearchAsync(Guid whatsAppNumberId, string query, int topK = 5, CancellationToken ct = default);
}