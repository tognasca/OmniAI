using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// ── Handoff ────────────────────────────────────────────────────────
namespace OmniAI.Application.Features.Handoff;

public class GetHandoffTriggersHandler(IOmniDbContext db)
    : IRequestHandler<GetHandoffTriggersQuery, IEnumerable<HandoffTriggerDto>>
{
    public async Task<IEnumerable<HandoffTriggerDto>> Handle(
        GetHandoffTriggersQuery req, CancellationToken ct)
    {
        return await db.HandoffTriggers
            .Where(t => t.WhatsAppNumberId == req.WhatsAppNumberId)
            .OrderByDescending(t => t.Priority)
            .Select(t => new HandoffTriggerDto(t.Id, t.WhatsAppNumberId, t.TriggerType, t.Value, t.Priority, t.IsActive))
            .ToListAsync(ct);
    }
}

// ✅ Handler adicionado — estava faltando
public class UpsertHandoffTriggerHandler(IOmniDbContext db)
    : IRequestHandler<UpsertHandoffTriggerCommand, HandoffTriggerDto>
{
    public async Task<HandoffTriggerDto> Handle(
        UpsertHandoffTriggerCommand req, CancellationToken ct)
    {
        HandoffTrigger trigger;

        if (req.Id.HasValue)
        {
            trigger = await db.HandoffTriggers.FindAsync([req.Id.Value], ct)
                ?? throw new InvalidOperationException($"Trigger {req.Id} not found.");
        }
        else
        {
            trigger = new HandoffTrigger { Id = Guid.NewGuid() };
            db.HandoffTriggers.Add(trigger);
        }

        trigger.WhatsAppNumberId = req.WhatsAppNumberId;
        trigger.TriggerType = req.TriggerType;
        trigger.Value = req.Value;
        trigger.Priority = req.Priority;
        trigger.IsActive = req.IsActive;
        trigger.TransitionMsg = req.TransitionMsg;

        await db.SaveChangesAsync(ct);

        return new HandoffTriggerDto(
            trigger.Id,
            trigger.WhatsAppNumberId,
            trigger.TriggerType,
            trigger.Value,
            trigger.Priority,
            trigger.IsActive);
    }
}

public class DeleteHandoffTriggerHandler(IOmniDbContext db)
    : IRequestHandler<DeleteHandoffTriggerCommand, bool>
{
    public async Task<bool> Handle(DeleteHandoffTriggerCommand req, CancellationToken ct)
    {
        var trigger = await db.HandoffTriggers.FindAsync([req.TriggerId], ct);
        if (trigger is null) return false;
        db.HandoffTriggers.Remove(trigger);
        await db.SaveChangesAsync(ct);
        return true;
    }
}