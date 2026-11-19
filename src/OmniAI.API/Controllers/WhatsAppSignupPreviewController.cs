using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application;

namespace OmniAI.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/whatsapp/signup-preview")]
public sealed class WhatsAppSignupPreviewController(IConfiguration configuration, IOmniDbContext db) : ControllerBase
{
    private const string Graph = "https://graph.facebook.com/v27.0";
    private static readonly HttpClient Meta = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    [HttpGet]
    public async Task<IActionResult> GetConfiguration(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await IsPilotAsync(ct))
            return NotFound();

        return Ok(new
        {
            appId = configuration["WhatsApp:SignupPreview:AppId"],
            configId = configuration["WhatsApp:SignupPreview:ConfigId"],
            graphVersion = "v27.0"
        });
    }

    [HttpPost]
    [RequestSizeLimit(8192)]
    public async Task<IActionResult> ValidateSignup(SignupPreviewRequest request, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!await IsPilotAsync(ct))
            return NotFound();

        var wabaId = configuration["WhatsApp:SignupPreview:SandboxWabaId"]!;
        var phoneId = configuration["WhatsApp:SignupPreview:SandboxPhoneId"]!;
        if (request.WabaId != wabaId || request.PhoneNumberId != phoneId)
            return BadRequest(new { error = "Use a conta e o número do sandbox configurado." });

        try
        {
            var url = QueryHelpers.AddQueryString(Graph + "/oauth/access_token", new Dictionary<string, string?>
            {
                ["client_id"] = configuration["WhatsApp:SignupPreview:AppId"],
                ["client_secret"] = configuration["WhatsApp:AppSecret"],
                ["code"] = request.Code
            });

            using var exchange = await Meta.GetAsync(url, ct);
            if (!exchange.IsSuccessStatusCode)
                return BadRequest(new { error = "A Meta recusou o código. Inicie novamente o teste." });

            using var token = JsonDocument.Parse(await exchange.Content.ReadAsStreamAsync(ct));
            if (!token.RootElement.TryGetProperty("access_token", out var value) ||
                value.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(value.GetString()))
                return StatusCode(502);

            using var check = new HttpRequestMessage(HttpMethod.Get, $"{Graph}/{wabaId}/phone_numbers?fields=id");
            check.Headers.Authorization = new AuthenticationHeaderValue("Bearer", value.GetString());
            using var response = await Meta.SendAsync(check, ct);
            if (!response.IsSuccessStatusCode)
                return StatusCode(502);

            using var phones = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            if (!phones.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                !data.EnumerateArray().Any(phone => phone.TryGetProperty("id", out var id) && id.GetString() == phoneId))
                return BadRequest(new { error = "A Meta não confirmou o número nesta conta de mensagens." });

            return Ok(new { status = "sandbox_verified" });
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return StatusCode(502, new { error = "Não foi possível validar o cadastro com a Meta." });
        }
    }

    private async Task<bool> IsPilotAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("WhatsApp:SignupPreview:Enabled") ||
            !Guid.TryParse(configuration["WhatsApp:SignupPreview:TenantId"], out var pilotId) ||
            !Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId) ||
            tenantId != pilotId ||
            !Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ||
            !IsMetaId(configuration["WhatsApp:SignupPreview:AppId"]) ||
            !IsMetaId(configuration["WhatsApp:SignupPreview:ConfigId"]) ||
            !IsMetaId(configuration["WhatsApp:SignupPreview:SandboxWabaId"]) ||
            !IsMetaId(configuration["WhatsApp:SignupPreview:SandboxPhoneId"]) ||
            string.IsNullOrWhiteSpace(configuration["WhatsApp:AppSecret"]))
            return false;

        return await db.AppUsers.AnyAsync(user => user.Id == userId && user.TenantId == pilotId &&
            user.IsActive && user.Tenant.IsActive && (user.Role == "Admin" || user.Role == "SuperAdmin"), ct);
    }

    private static bool IsMetaId(string? value) =>
        value is { Length: > 0 and <= 50 } && value.All(char.IsAsciiDigit);
}

public sealed record SignupPreviewRequest(
    [Required, StringLength(4096)] string Code,
    [Required, StringLength(50)] string WabaId,
    [Required, StringLength(50)] string PhoneNumberId);
