using System.Text.Json.Serialization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OmniAI.Domain.Interfaces;

namespace OmniAI.Infrastructure.Services.AI;

public class OpenAiService(
    HttpClient http,
    IConfiguration cfg,
    ILogger<OpenAiService> log) : IAiService
{
    private string ApiKey => cfg["OpenAI:ApiKey"]!;
    private string Model   => cfg["OpenAI:Model"] ?? "gpt-4o";

    public async Task<string> GetCompletionAsync(
        string systemPrompt,
        IEnumerable<(string Role, string Content)> history,
        string userMessage,
        CancellationToken ct = default)
    {
        return (await CompleteAsync(new AiCompletionRequest(
            systemPrompt, history.ToList(), userMessage, Model,
            cfg.GetValue<int?>("OpenAI:MaxTokens") ?? 1000,
            cfg.GetValue<float?>("OpenAI:Temperature") ?? 0.3f), ct)).Text;
    }

    public async Task<AiCompletion> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var model = string.IsNullOrWhiteSpace(request.Model) ? Model : request.Model;
        var maxTokens = Math.Clamp(request.MaxTokens, 1, 4096);
        var temperature = Math.Clamp(request.Temperature, 0, 1);
        if (model.StartsWith("claude-", StringComparison.Ordinal))
            return await CallClaudeAsync(request, model, maxTokens, temperature, ct);

        var messages = new List<object>
        {
            new { role = "system", content = request.SystemPrompt }
        };

        foreach (var (role, content) in request.History)
            messages.Add(new { role, content });

        messages.Add(new { role = "user", content = request.UserMessage });

        var payload = new
        {
            model,
            messages,
            max_completion_tokens = maxTokens,
            temperature
        };

        var reply = await CallOpenAiAsync<ChatCompletion>(
            "https://api.openai.com/v1/chat/completions", payload, ct);

        return new AiCompletion(reply?.Choices?.FirstOrDefault()?.Message?.Content ?? "",
            reply?.Model ?? model, reply?.Usage?.InputTokens ?? 0, reply?.Usage?.OutputTokens ?? 0);
    }

    private async Task<AiCompletion> CallClaudeAsync(AiCompletionRequest request, string model,
        int maxTokens, float temperature, CancellationToken ct)
    {
        var key = cfg["Anthropic:ApiKey"];
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Anthropic credentials are not configured.");

        using var post = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        post.Headers.Add("x-api-key", key);
        post.Headers.Add("anthropic-version", "2023-06-01");
        post.Content = System.Net.Http.Json.JsonContent.Create(new
        {
            model,
            system = request.SystemPrompt,
            messages = request.History.Append(("user", request.UserMessage))
                .Select(message => new { role = message.Item1, content = message.Item2 }),
            max_tokens = maxTokens,
            temperature
        });

        using var response = await http.SendAsync(post, ct);
        if (!response.IsSuccessStatusCode)
            log.LogWarning("Anthropic API returned HTTP {Status}", (int)response.StatusCode);
        response.EnsureSuccessStatusCode();
        await using var body = await response.Content.ReadAsStreamAsync(ct);
        var reply = await JsonSerializer.DeserializeAsync<ClaudeCompletion>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        return new AiCompletion(string.Concat(reply?.Content?.Where(block => block.Type == "text")
            .Select(block => block.Text) ?? []), reply?.Model ?? model,
            reply?.Usage?.InputTokens ?? 0, reply?.Usage?.OutputTokens ?? 0);
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var payload = new
        {
            model = "text-embedding-3-large",
            input = text,
            dimensions = 1536
        };

        var result = await CallOpenAiAsync<EmbeddingResponse>(
            "https://api.openai.com/v1/embeddings", payload, ct);

        return result?.Data?.FirstOrDefault()?.Embedding ?? [];
    }

    public async Task<bool> ShouldHandoffAsync(
        string message,
        IEnumerable<string> triggers,
        CancellationToken ct = default)
    {
        var triggerList = string.Join(", ", triggers);
        var prompt = $"Given this message: \"{message}\"\n\n" +
                     $"Does it match any of these handoff triggers: [{triggerList}]?\n" +
                     $"Reply ONLY with 'yes' or 'no'.";

        var result = await GetCompletionAsync(
            "You are a message classifier. Reply only yes or no.",
            [], prompt, ct);

        return result.Trim().ToLower().StartsWith("yes");
    }

    private async Task<T?> CallOpenAiAsync<T>(string url, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        var json = JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("OpenAI API returned HTTP {Status}", (int)response.StatusCode);
            response.EnsureSuccessStatusCode();
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private record ChatCompletion(Choice[]? Choices, string? Model, OpenAiUsage? Usage);
    private record OpenAiUsage([property: JsonPropertyName("prompt_tokens")] int InputTokens,
        [property: JsonPropertyName("completion_tokens")] int OutputTokens);
    private record ClaudeCompletion(ClaudeBlock[]? Content, string? Model, ClaudeUsage? Usage);
    private record ClaudeBlock(string Type, string? Text);
    private record ClaudeUsage([property: JsonPropertyName("input_tokens")] int InputTokens,
        [property: JsonPropertyName("output_tokens")] int OutputTokens);
    private record Choice(ChoiceMessage Message);
    private record ChoiceMessage(string Role, string Content);
    private record EmbeddingResponse(EmbeddingData[]? Data);
    private record EmbeddingData(float[] Embedding);
}
