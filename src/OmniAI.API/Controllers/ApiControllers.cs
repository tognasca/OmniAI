using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Contracts.Responses;
using OmniAI.Domain.Enums;

namespace OmniAI.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ConversationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ConversationDto>>> GetAll(
        [FromQuery] Guid tenantId,
        [FromQuery] ConversationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetConversationsQuery(tenantId, status, page, pageSize), ct);
        return Ok(result);
    }

    [HttpGet("{id}/messages")]
    public async Task<ActionResult<PagedResult<MessageDto>>> GetMessages(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetConversationMessagesQuery(id, page, pageSize), ct);
        return Ok(result);
    }

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignAgentRequest req, CancellationToken ct = default)
    {
        await mediator.Send(new AssignAgentCommand(id, req.AgentId, req.AgentName), ct);
        return Ok();
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseConversationRequest req, CancellationToken ct = default)
    {
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
public class WhatsAppNumbersController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WhatsAppNumberDto>>> GetAll(
        [FromQuery] Guid tenantId, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetNumbersQuery(tenantId), ct));

    [HttpPost]
    public async Task<ActionResult<WhatsAppNumberDto>> Add(
        [FromBody] AddWhatsAppNumberCommand cmd, CancellationToken ct = default)
    {
        var result = await mediator.Send(cmd, ct);
        return CreatedAtAction(nameof(GetAll), new { tenantId = result.TenantId }, result);
    }
}

// ── Bot Config ───────────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/bot-config")]
public class BotConfigController(IMediator mediator) : ControllerBase
{
    [HttpGet("{numberId}")]
    public async Task<ActionResult<BotConfigDto>> Get(Guid numberId, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetBotConfigQuery(numberId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPut]
    public async Task<ActionResult<BotConfigDto>> Upsert(
        [FromBody] UpsertBotConfigCommand cmd, CancellationToken ct = default)
        => Ok(await mediator.Send(cmd, ct));
}

// ── Knowledge Base ───────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/knowledge")]
public class KnowledgeController(IMediator mediator) : ControllerBase
{
    [HttpGet("{numberId}")]
    public async Task<ActionResult<IEnumerable<KnowledgeSourceDto>>> GetAll(
        Guid numberId, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetKnowledgeSourcesQuery(numberId), ct));

    [HttpPost("text")]
    public async Task<ActionResult<KnowledgeSourceDto>> AddText(
        [FromBody] AddKnowledgeTextCommand cmd, CancellationToken ct = default)
        => CreatedAtAction(nameof(GetAll), new { numberId = cmd.WhatsAppNumberId },
            await mediator.Send(cmd, ct));

    [HttpPost("url")]
    public async Task<ActionResult<KnowledgeSourceDto>> AddUrl(
        [FromBody] AddKnowledgeUrlCommand cmd, CancellationToken ct = default)
        => CreatedAtAction(nameof(GetAll), new { numberId = cmd.WhatsAppNumberId },
            await mediator.Send(cmd, ct));

    [HttpDelete("{sourceId}")]
    public async Task<IActionResult> Delete(Guid sourceId, CancellationToken ct = default)
    {
        var ok = await mediator.Send(new DeleteKnowledgeSourceCommand(sourceId), ct);
        return ok ? NoContent() : NotFound();
    }
}

// ── Handoff Triggers ─────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/handoff-triggers")]
public class HandoffTriggersController(IMediator mediator) : ControllerBase
{
    [HttpGet("{numberId}")]
    public async Task<ActionResult<IEnumerable<HandoffTriggerDto>>> GetAll(
        Guid numberId, CancellationToken ct = default)
        => Ok(await mediator.Send(new GetHandoffTriggersQuery(numberId), ct));

    [HttpPut]
    public async Task<ActionResult<HandoffTriggerDto>> Upsert(
        [FromBody] UpsertHandoffTriggerCommand cmd, CancellationToken ct = default)
        => Ok(await mediator.Send(cmd, ct));

    [HttpDelete("{triggerId}")]
    public async Task<IActionResult> Delete(Guid triggerId, CancellationToken ct = default)
    {
        var ok = await mediator.Send(new DeleteHandoffTriggerCommand(triggerId), ct);
        return ok ? NoContent() : NotFound();
    }
}

// ── Metrics ──────────────────────────────────────────────────────

[ApiController]
[Authorize]
[Route("api/metrics")]
public class MetricsController(IMediator mediator) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardMetricsDto>> GetDashboard(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate   = to   ?? DateTime.UtcNow;
        var result   = await mediator.Send(new GetDashboardMetricsQuery(tenantId, fromDate, toDate), ct);
        return Ok(result);
    }
}
