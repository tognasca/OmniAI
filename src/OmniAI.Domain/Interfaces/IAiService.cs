namespace OmniAI.Domain.Interfaces;

public interface IAiService
{
    Task<string> GetCompletionAsync(string systemPrompt, IEnumerable<(string Role, string Content)> history, string userMessage, CancellationToken ct = default);
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
    Task<bool> ShouldHandoffAsync(string message, IEnumerable<string> triggers, CancellationToken ct = default);
}