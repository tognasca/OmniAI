using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application;
using OmniAI.Domain.Entities;
using OmniAI.API;

namespace OmniAI.API.Controllers;

[ApiController]
[Route("v1")]
[ApiExplorerSettings(GroupName = "public-v1")]
public sealed class PublicReadController(IOmniDbContext db, IConfiguration config, PublicApiQuota quota) : ControllerBase
{
    [HttpGet("tenant")]
    public async Task<IActionResult> GetTenant(CancellationToken ct)
    {
        var (tenant, error) = await AuthenticateAsync("tenant:read", ct);
        return tenant is null ? error! : Ok(new { tenant.Id, tenant.Name, tenant.Slug });
    }

    [HttpGet("contacts")]
    public async Task<IActionResult> GetContacts([FromQuery, Range(0, 100000)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 25, CancellationToken ct = default)
    {
        var (tenant, error) = await AuthenticateAsync("contacts:read", ct);
        if (tenant is null) return error!;

        return Ok(await db.Contacts.AsNoTracking()
            .Where(c => c.TenantId == tenant.Id)
            .OrderBy(c => c.Id).Skip(offset).Take(limit)
            .Select(c => new { c.Id, c.Name, c.PhoneNumber, c.FirstSeenAt, c.LastSeenAt, c.IsBlocked })
            .ToListAsync(ct));
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations([FromQuery, Range(0, 100000)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 25, CancellationToken ct = default)
    {
        var (tenant, error) = await AuthenticateAsync("conversations:read", ct);
        if (tenant is null) return error!;

        return Ok(await db.Conversations.AsNoTracking()
            .Where(c => c.Number.TenantId == tenant.Id && c.Contact.TenantId == tenant.Id)
            .OrderByDescending(c => c.StartedAt).ThenBy(c => c.Id).Skip(offset).Take(limit)
            .Select(c => new { c.Id, c.ContactId, c.WhatsAppNumberId, c.Status, c.StartedAt, c.ClosedAt })
            .ToListAsync(ct));
    }

    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid conversationId,
        [FromQuery, Range(0, 100000)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 25, CancellationToken ct = default)
    {
        var (tenant, error) = await AuthenticateAsync("messages:read", ct);
        if (tenant is null) return error!;

        var conversation = db.Conversations.AsNoTracking().Where(c => c.Id == conversationId &&
            c.Number.TenantId == tenant.Id && c.Contact.TenantId == tenant.Id);
        if (!await conversation.AnyAsync(ct))
            return Problem(statusCode: 404, title: "Conversa não encontrada");

        return Ok(await db.Messages.AsNoTracking()
            .Where(m => conversation.Any(c => c.Id == m.ConversationId))
            .OrderByDescending(m => m.SentAt).ThenBy(m => m.Id).Skip(offset).Take(limit)
            .Select(m => new { m.Id, m.Direction, m.Type, m.Content, m.SentAt, m.DeliveredAt, m.ReadAt })
            .ToListAsync(ct));
    }

    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage(CancellationToken ct)
    {
        var (tenant, error) = await AuthenticateAsync("usage:read", ct);
        if (tenant is null) return error!;

        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var usage = await db.AiUsageLogs.AsNoTracking()
            .Where(u => u.CreatedAt >= start && u.CreatedAt < end &&
                u.Message.Conversation.Number.TenantId == tenant.Id &&
                u.Message.Conversation.Contact.TenantId == tenant.Id &&
                u.WhatsAppNumberId == u.Message.Conversation.WhatsAppNumberId)
            .GroupBy(_ => 1)
            .Select(g => new { Requests = g.Count(), Tokens = g.Sum(x => (long)x.TotalTokens), CostUsd = g.Sum(x => x.CostUsd) })
            .SingleOrDefaultAsync(ct);
        return Ok(new { start, end, requests = usage?.Requests ?? 0, tokens = usage?.Tokens ?? 0, aiCostUsd = usage?.CostUsd ?? 0m });
    }

    private async Task<(Tenant? Tenant, IActionResult? Error)> AuthenticateAsync(string scope, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!config.GetValue<bool>("PublicApi:Enabled") ||
            !Request.Headers.TryGetValue("X-Api-Key", out var header) || header.Count != 1)
            return (null, UnauthorizedKey());

        var presented = header[0];
        if (presented is null || presented.Length is < 44 or > 128)
            return (null, UnauthorizedKey());

        var parts = presented.Split('.', 2);
        if (parts.Length != 2 || !IsKeyPart(parts[0], 1, 32) || !IsKeyPart(parts[1], 43, 95))
            return (null, UnauthorizedKey());

        var key = config.GetSection("PublicApi:Keys").GetSection(parts[0]);
        if (!key.GetValue<bool>("Enabled") ||
            !Guid.TryParse(key["TenantId"], out var tenantId) ||
            !DateTimeOffset.TryParse(key["ExpiresAt"], System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal, out var expiry) ||
            expiry <= DateTimeOffset.UtcNow ||
            key["Scopes"]?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains(scope, StringComparer.Ordinal) != true ||
            !MatchesHash(parts[1], key["Sha256"]))
            return (null, UnauthorizedKey());

        var requestsPerMinute = key.GetValue<int?>("RequestsPerMinute") ?? 60;
        if (requestsPerMinute is < 1 or > 6000)
            return (null, UnauthorizedKey());

        using var lease = quota.Acquire(parts[0], requestsPerMinute);
        if (!lease.IsAcquired)
        {
            if (lease.TryGetMetadata(System.Threading.RateLimiting.MetadataName.RetryAfter, out var retryAfter))
                Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retryAfter.TotalSeconds))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
            return (null, Problem(statusCode: 429, title: "Limite de requisições atingido"));
        }

        var tenant = await db.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct);
        return tenant is null ? (null, UnauthorizedKey()) : (tenant, null);
    }

    private static bool IsKeyPart(string value, int min, int max) =>
        value.Length >= min && value.Length <= max && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static bool MatchesHash(string secret, string? expected)
    {
        if (expected is not { Length: 64 }) return false;
        byte[] hash;
        try { hash = Convert.FromHexString(expected); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(hash, SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
    }

    private ObjectResult UnauthorizedKey() =>
        Problem(statusCode: 401, title: "Chave de API inválida ou sem acesso");
}
