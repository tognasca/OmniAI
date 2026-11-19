using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Contracts.Responses;
using OmniAI.Domain.Enums;

namespace OmniAI.API.Controllers;

public abstract class TenantControllerBase(IOmniDbContext db) : ControllerBase
{
    protected Guid? TenantId => Guid.TryParse(User.FindFirstValue("tenantId"), out var value)
        ? value : null;

    protected Task<bool> IsActiveTenantAsync(Guid id, CancellationToken ct) =>
        db.Tenants.AnyAsync(t => t.Id == id && t.IsActive, ct);

    protected IQueryable<OmniAI.Domain.Entities.WhatsAppNumber> TenantNumbers =>
        db.WhatsAppNumbers.Where(n => n.TenantId == TenantId && n.Tenant.IsActive);

    protected Task<bool> OwnsNumberAsync(Guid numberId, CancellationToken ct) =>
        TenantNumbers.AnyAsync(n => n.Id == numberId, ct);

    protected Task<bool> OwnsConversationAsync(Guid conversationId, CancellationToken ct) =>
        db.Conversations.AnyAsync(c => c.Id == conversationId && c.Contact.TenantId == TenantId &&
            TenantNumbers.Any(n => n.Id == c.WhatsAppNumberId), ct);

    protected Task<bool> OwnsSourceAsync(Guid sourceId, CancellationToken ct) =>
        db.KnowledgeSources.AnyAsync(s => s.Id == sourceId &&
            TenantNumbers.Any(n => n.Id == s.WhatsAppNumberId), ct);

    protected Task<bool> OwnsTriggerAsync(Guid triggerId, Guid? numberId, CancellationToken ct) =>
        db.HandoffTriggers.AnyAsync(t => t.Id == triggerId &&
            (numberId == null || t.WhatsAppNumberId == numberId) &&
            TenantNumbers.Any(n => n.Id == t.WhatsAppNumberId), ct);
}


[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ConversationsController(IMediator mediator, IOmniDbContext db) : TenantControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ConversationDto>>> GetAll(
        [FromQuery] Guid tenantId,
        [FromQuery] ConversationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (TenantId is not { } id || !await IsActiveTenantAsync(id, ct)) return Forbid();
        var result = await mediator.Send(new GetConversationsQuery(id, status, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id}/messages")]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        if (!await OwnsConversationAsync(id, ct)) return NotFound();
        var result = await mediator.Send(new GetConversationMessagesQuery(id, page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignAgentRequest req, CancellationToken ct = default)
    {
        if (!await OwnsConversationAsync(id, ct)) return NotFound();
        await mediator.Send(new AssignAgentCommand(id, req.AgentId, req.AgentName), ct);
        return Ok();
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseConversationRequest req, CancellationToken ct = default)
    {
        if (!await OwnsConversationAsync(id, ct)) return NotFound();
        await mediator.Send(new CloseConversationCommand(id, req.CsatScore, req.CsatComment), ct);
        return Ok();
    }
}

public record AssignAgentRequest(Guid AgentId, string AgentName);
public record CloseConversationRequest(int? CsatScore, string? CsatComment);

// ── WhatsApp Numbers ─────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/numbers")]
public class WhatsAppNumbersController(IMediator mediator, IOmniDbContext db) : TenantControllerBase(db)
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WhatsAppNumberDto>>> GetAll(
        [FromQuery] Guid tenantId, CancellationToken ct = default)
    {
        if (TenantId is not { } id || !await IsActiveTenantAsync(id, ct)) return Forbid();
        return Ok(await mediator.Send(new GetNumbersQuery(id), ct));
    }

    [HttpPost]
    public async Task<ActionResult<WhatsAppNumberDto>> Add(
        [FromBody] AddWhatsAppNumberCommand cmd, CancellationToken ct = default)
    {
        if (TenantId is not { } id || id != cmd.TenantId || !await IsActiveTenantAsync(id, ct)) return Forbid();
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { tenantId = result.TenantId }, result);
    }
}

// ── Bot Config ───────────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/bot-config")]
public class BotConfigController(IMediator mediator, IOmniDbContext db) : TenantControllerBase(db)
{
    [HttpGet("{numberId}")]
    public async Task<ActionResult<BotConfigDto>> Get(Guid numberId, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(numberId, ct)) return NotFound();
        var result = await mediator.Send(new GetBotConfigQuery(numberId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut]
    [Authorize(Roles = "Admin,SuperAdmin")]
    public async Task<ActionResult<BotConfigDto>> Upsert(
        [FromBody] UpsertBotConfigCommand cmd, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(cmd.WhatsAppNumberId, ct)) return NotFound();
        return Ok(await mediator.Send(cmd, ct));
    }

}

// ── Knowledge Base ───────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/knowledge")]
public class KnowledgeController(IMediator mediator, IOmniDbContext db) : TenantControllerBase(db)
{
    [HttpGet("{numberId}")]
    public async Task<ActionResult<IEnumerable<KnowledgeSourceDto>>> GetAll(
        Guid numberId, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(numberId, ct)) return NotFound();
        return Ok(await mediator.Send(new GetKnowledgeSourcesQuery(numberId), ct));
    }

    [HttpPost("text")]
    public async Task<ActionResult<KnowledgeSourceDto>> AddText(
        [FromBody] AddKnowledgeTextCommand cmd, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(cmd.WhatsAppNumberId, ct)) return NotFound();
        return CreatedAtAction(nameof(GetAll), new { numberId = cmd.WhatsAppNumberId },
            await mediator.Send(cmd, ct));
    }

    [HttpPost("url")]
    public async Task<ActionResult<KnowledgeSourceDto>> AddUrl(
        [FromBody] AddKnowledgeUrlCommand cmd, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(cmd.WhatsAppNumberId, ct)) return NotFound();
        return CreatedAtAction(nameof(GetAll), new { numberId = cmd.WhatsAppNumberId },
            await mediator.Send(cmd, ct));
    }

    [HttpDelete("{sourceId}")]
    public async Task<IActionResult> Delete(Guid sourceId, CancellationToken ct = default)
    {
        if (!await OwnsSourceAsync(sourceId, ct)) return NotFound();
        var ok = await mediator.Send(new DeleteKnowledgeSourceCommand(sourceId), ct);
        return ok ? NoContent() : NotFound();
    }
}

// ── Handoff Triggers ─────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/handoff-triggers")]
public class HandoffTriggersController(IMediator mediator, IOmniDbContext db) : TenantControllerBase(db)
{
    [HttpGet("{numberId}")]
    public async Task<ActionResult<IEnumerable<HandoffTriggerDto>>> GetAll(
        Guid numberId, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(numberId, ct)) return NotFound();
        return Ok(await mediator.Send(new GetHandoffTriggersQuery(numberId), ct));
    }

    [HttpPut]
    public async Task<ActionResult<HandoffTriggerDto>> Upsert(
        [FromBody] UpsertHandoffTriggerCommand cmd, CancellationToken ct = default)
    {
        if (!await OwnsNumberAsync(cmd.WhatsAppNumberId, ct)) return NotFound();
        if (cmd.Id is { } id && !await OwnsTriggerAsync(id, cmd.WhatsAppNumberId, ct)) return NotFound();
        return Ok(await mediator.Send(cmd, ct));
    }

    [HttpDelete("{triggerId}")]
    public async Task<IActionResult> Delete(Guid triggerId, CancellationToken ct = default)
    {
        if (!await OwnsTriggerAsync(triggerId, null, ct)) return NotFound();
        var ok = await mediator.Send(new DeleteHandoffTriggerCommand(triggerId), ct);
        return ok ? NoContent() : NotFound();
    }
}

// ── Metrics ──────────────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/metrics")]
public class MetricsController(IMediator mediator, IOmniDbContext db) : TenantControllerBase(db)
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardMetricsDto>> GetDashboard(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        if (TenantId is not { } id || !await IsActiveTenantAsync(id, ct)) return Forbid();
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate   = to   ?? DateTime.UtcNow;
        var result   = await mediator.Send(new GetDashboardMetricsQuery(id, fromDate, toDate), ct);
        return Ok(result);
    }
}
