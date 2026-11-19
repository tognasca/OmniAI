namespace OmniAI.Domain.Interfaces;

public interface IAiService
{
    Task<string> GetCompletionAsync(string systemPrompt, IEnumerable<(string Role, string Content)> history, string userMessage, CancellationToken ct = default);
    async Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default) =>
        new(await GetCompletionAsync(request.SystemPrompt, request.History, request.UserMessage, ct),
            request.Model, 0, 0);

    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
    Task<bool> ShouldHandoffAsync(string message, IEnumerable<string> triggers, CancellationToken ct = default);
}
public sealed record AiCompletionRequest(
    string SystemPrompt, IReadOnlyList<(string Role, string Content)> History, string UserMessage,
    string Model, int MaxTokens, float Temperature);

public sealed record AiCompletion(string Text, string Model, int InputTokens, int OutputTokens);
