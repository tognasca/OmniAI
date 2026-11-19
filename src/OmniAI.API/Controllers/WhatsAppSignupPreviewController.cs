using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application;
using OmniAI.API.WhatsApp;

namespace OmniAI.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,SuperAdmin")]
[Route("api/whatsapp/signup-preview")]
public sealed class WhatsAppSignupPreviewController(IConfiguration configuration, IOmniDbContext db, MetaSignupClient meta) : ControllerBase
{
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
            graphVersion = MetaSignupClient.GraphVersion
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

        var result = await meta.ValidateAsync(
            configuration["WhatsApp:SignupPreview:AppId"]!,
            configuration["WhatsApp:AppSecret"]!,
            request.Code, wabaId, phoneId, ct);

        return result switch
        {
            MetaSignupResult.Verified => Ok(new { status = "sandbox_verified" }),
            MetaSignupResult.CodeRejected => BadRequest(new { error = "A Meta recusou o código. Inicie novamente o teste." }),
            MetaSignupResult.PhoneMismatch => BadRequest(new { error = "A Meta não confirmou o número nesta conta de mensagens." }),
            _ => StatusCode(502, new { error = "Não foi possível validar o cadastro com a Meta." })
        };
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
