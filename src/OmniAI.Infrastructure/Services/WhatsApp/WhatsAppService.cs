using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniAI.Application;
using OmniAI.Domain.Interfaces;

namespace OmniAI.Infrastructure.Services.WhatsApp;

public class WhatsAppService(
    HttpClient http,
    IOmniDbContext db,
    ILogger<WhatsAppService> log) : IWhatsAppService
{
    private const string ApiBase = "https://graph.facebook.com/v19.0";

    public async Task SendTextAsync(Guid numberId, string to, string text, CancellationToken ct = default)
    {
        var number = await db.WhatsAppNumbers.FindAsync([numberId], ct)
                    ?? throw new KeyNotFoundException($"Number {numberId} not found");

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to,
            type = "text",
            text = new { body = text, preview_url = false }
        };

        await PostAsync(number.PhoneNumberId, number.AccessToken, payload, ct);
        log.LogInformation("Sent text to {To} via number {NumberId}", to, numberId);
    }

    public async Task SendTemplateAsync(Guid numberId, string to, string templateName,
        object[] components, CancellationToken ct = default)
    {
        var number = await db.WhatsAppNumbers.FindAsync([numberId], ct)
                    ?? throw new KeyNotFoundException();

        var payload = new
        {
            messaging_product = "whatsapp",
            to,
            type = "template",
            template = new
            {
                name = templateName,
                language = new { code = "pt_BR" },
                components
            }
        };

        await PostAsync(number.PhoneNumberId, number.AccessToken, payload, ct);
    }

    public async Task MarkAsReadAsync(Guid numberId, string waMessageId, CancellationToken ct = default)
    {
        var number = await db.WhatsAppNumbers.FindAsync([numberId], ct)
                    ?? throw new KeyNotFoundException();

        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = waMessageId
        };

        await PostAsync(number.PhoneNumberId, number.AccessToken, payload, ct);
    }

    private async Task PostAsync(string phoneNumberId, string token, object payload, CancellationToken ct)
    {
        var url = $"{ApiBase}/{phoneNumberId}/messages";
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await http.PostAsync(url, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            log.LogError("Meta API error {Status}: {Error}", response.StatusCode, error);
            response.EnsureSuccessStatusCode();
        }
    }
}
