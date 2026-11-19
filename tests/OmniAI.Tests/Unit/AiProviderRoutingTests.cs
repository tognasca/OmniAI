using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OmniAI.Domain.Interfaces;
using OmniAI.Infrastructure.Services.AI;
using Xunit;

namespace OmniAI.Tests.Unit;

public sealed class AiProviderRoutingTests
{
    [Theory]
    [InlineData("gpt-4o-mini", "api.openai.com", "{\"choices\":[{\"message\":{\"content\":\"Olá\"}}],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3}}")]
    [InlineData("claude-haiku-5-5", "api.anthropic.com", "{\"content\":[{\"type\":\"text\",\"text\":\"Olá\"}],\"usage\":{\"input_tokens\":7,\"output_tokens\":3}}")]
    public async Task RoutesConfiguredModelAndReadsTokenCounts(string model, string host, string json)
    {
        using var handler = new ProviderHandler(host, model, json);
        using var http = new HttpClient(handler);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "openai-test",
            ["Anthropic:ApiKey"] = "anthropic-test"
        }).Build();
        var service = new OpenAiService(http, config, NullLogger<OpenAiService>.Instance);

        var reply = await service.CompleteAsync(new AiCompletionRequest("Sistema", [], "Olá", model, 250, 0.2f));

        Assert.Equal(new AiCompletion("Olá", model, 7, 3), reply);
    }

    private sealed class ProviderHandler(string host, string model, string json) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal(host, request.RequestUri?.Host);
            var payload = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains(model, payload);
            Assert.Contains("250", payload);
            if (host == "api.anthropic.com")
            {
                Assert.Equal("anthropic-test", Assert.Single(request.Headers.GetValues("x-api-key")));
                Assert.Null(request.Headers.Authorization);
            }
            else
            {
                Assert.Equal("openai-test", request.Headers.Authorization?.Parameter);
                Assert.False(request.Headers.Contains("x-api-key"));
            }
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
