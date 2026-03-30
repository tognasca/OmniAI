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
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt }
        };

        foreach (var (role, content) in history)
            messages.Add(new { role, content });

        messages.Add(new { role = "user", content = userMessage });

        var payload = new
        {
            model = Model,
            messages,
            max_tokens = int.TryParse(cfg["OpenAI:MaxTokens"], out var mt) ? mt : 1000,
            temperature = double.TryParse(cfg["OpenAI:Temperature"], out var t) ? t : 0.3
        };

        var reply = await CallOpenAiAsync<ChatCompletion>(
            "https://api.openai.com/v1/chat/completions", payload, ct);

        return reply?.Choices?.FirstOrDefault()?.Message?.Content ?? "";
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
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ApiKey);

        var json = JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await http.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            log.LogError("OpenAI API error {Status}: {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private record ChatCompletion(Choice[]? Choices);
    private record Choice(ChoiceMessage Message);
    private record ChoiceMessage(string Role, string Content);
    private record EmbeddingResponse(EmbeddingData[]? Data);
    private record EmbeddingData(float[] Embedding);
}
