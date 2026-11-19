using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace OmniAI.API.WhatsApp;

public enum MetaSignupResult { Verified, CodeRejected, PhoneMismatch, UpstreamFailure }

public sealed class MetaSignupClient(HttpClient http)
{
    public const string GraphVersion = "v27.0";
    private const string Graph = "https://graph.facebook.com/" + GraphVersion;

    public async Task<MetaSignupResult> ValidateAsync(
        string appId, string appSecret, string code, string wabaId, string phoneId, CancellationToken ct)
    {
        try
        {
            var url = QueryHelpers.AddQueryString(Graph + "/oauth/access_token", new Dictionary<string, string?>
            {
                ["client_id"] = appId,
                ["client_secret"] = appSecret,
                ["code"] = code
            });

            using var exchange = await http.GetAsync(url, ct);
            if (!exchange.IsSuccessStatusCode)
                return exchange.StatusCode == System.Net.HttpStatusCode.BadRequest
                    ? MetaSignupResult.CodeRejected
                    : MetaSignupResult.UpstreamFailure;

            using var token = await JsonDocument.ParseAsync(await exchange.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (!TryString(token.RootElement, "access_token", out var value) || string.IsNullOrWhiteSpace(value))
                return MetaSignupResult.UpstreamFailure;

            using var check = new HttpRequestMessage(HttpMethod.Get, $"{Graph}/{wabaId}/phone_numbers?fields=id");
            check.Headers.Authorization = new AuthenticationHeaderValue("Bearer", value);
            using var response = await http.SendAsync(check, ct);
            if (!response.IsSuccessStatusCode)
                return MetaSignupResult.UpstreamFailure;

            using var phones = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            if (phones.RootElement.ValueKind != JsonValueKind.Object ||
                !phones.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
                return MetaSignupResult.UpstreamFailure;

            return data.EnumerateArray().Any(phone => TryString(phone, "id", out var id) && id == phoneId)
                ? MetaSignupResult.Verified
                : MetaSignupResult.PhoneMismatch;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return MetaSignupResult.UpstreamFailure;
        }
    }

    private static bool TryString(JsonElement element, string name, out string? value)
    {
        value = element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        return value is not null;
    }
}
