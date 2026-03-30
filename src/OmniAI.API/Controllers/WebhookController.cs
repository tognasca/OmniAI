using Microsoft.AspNetCore.Mvc;
using OmniAI.Infrastructure.BackgroundWorkers;
using System.Text.Json.Serialization;

namespace OmniAI.API.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController(IncomingMessageQueue queue, IConfiguration cfg, ILogger<WebhookController> logger) : ControllerBase
{
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string token,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        if (mode == "subscribe" && token == cfg["WhatsApp:VerifyToken"])
            return Ok(challenge);
        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] MetaWebhookPayload payload)
    {
        try
        {
            foreach (var entry in payload.Entry ?? [])
                foreach (var change in entry.Changes ?? [])
                {
                    if (change.Field != "messages") continue;
                    foreach (var msg in change.Value?.Messages ?? [])
                    {
                        var content = msg.Type == "text" ? msg.Text?.Body : $"[{msg.Type}]";
                        await queue.EnqueueAsync(new IncomingMessageJob(
                            change.Value?.Metadata?.PhoneNumberId ?? "",
                            msg.Id, msg.From, msg.Type, content, msg.Image?.Id));
                    }
                }
        }
        catch (Exception ex) { logger.LogError(ex, "Webhook parse error"); }
        return Ok();
    }
}

public record MetaWebhookPayload(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] List<WebhookEntry>? Entry);
public record WebhookEntry([property: JsonPropertyName("id")] string? Id, [property: JsonPropertyName("changes")] List<WebhookChange>? Changes);
public record WebhookChange([property: JsonPropertyName("field")] string? Field, [property: JsonPropertyName("value")] WebhookValue? Value);
public record WebhookValue(
    [property: JsonPropertyName("metadata")] WebhookMetadata? Metadata,
    [property: JsonPropertyName("messages")] List<WhMsg>? Messages);
public record WebhookMetadata([property: JsonPropertyName("phone_number_id")] string? PhoneNumberId);
public record WhMsg(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] WhText? Text,
    [property: JsonPropertyName("image")] WhMedia? Image);
public record WhText([property: JsonPropertyName("body")] string? Body);
public record WhMedia([property: JsonPropertyName("id")] string? Id);
