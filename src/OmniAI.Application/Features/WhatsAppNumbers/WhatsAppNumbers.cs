using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Application.Contracts.Responses;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;

namespace OmniAI.Application.Features.WhatsAppNumbers;

// ── Shared helper: resolve TenantId ───────────────────────────────
// Se o frontend envia Guid.Empty (VITE_TENANT_ID não configurado),
// busca automaticamente o primeiro tenant ativo do banco.
internal static class TenantHelper
{
    internal static async Task<Guid> ResolveAsync(
        IOmniDbContext db, Guid requested, CancellationToken ct)
    {
        if (requested != Guid.Empty) return requested;

        var id = await db.Tenants
            .Where(t => t.IsActive)
            .OrderBy(t => t.CreatedAt)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);

        return id ?? throw new InvalidOperationException(
            "No active tenant found. Run the database seed first.");
    }
}

// ── GET numbers ───────────────────────────────────────────────────
public class GetNumbersHandler(IOmniDbContext db)
    : IRequestHandler<GetNumbersQuery, IEnumerable<WhatsAppNumberDto>>
{
    public async Task<IEnumerable<WhatsAppNumberDto>> Handle(
        GetNumbersQuery req, CancellationToken ct)
    {
        var tenantId = await TenantHelper.ResolveAsync(db, req.TenantId, ct);

        return await db.WhatsAppNumbers
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new WhatsAppNumberDto(
                n.Id, n.TenantId, n.PhoneNumberId, n.WabaId,
                n.DisplayName, n.Status, n.ConnectedAt))
            .ToListAsync(ct);
    }
}

// ── ADD number ────────────────────────────────────────────────────
public class AddWhatsAppNumberHandler(IOmniDbContext db)
    : IRequestHandler<AddWhatsAppNumberCommand, WhatsAppNumberDto>
{
    public async Task<WhatsAppNumberDto> Handle(
        AddWhatsAppNumberCommand req, CancellationToken ct)
    {
        var tenantId = await TenantHelper.ResolveAsync(db, req.TenantId, ct);

        var exists = await db.WhatsAppNumbers
            .AnyAsync(n => n.TenantId == tenantId
                        && n.PhoneNumberId == req.PhoneNumberId, ct);

        if (exists)
            throw new InvalidOperationException(
                $"Phone number {req.PhoneNumberId} already registered.");

        var number = new WhatsAppNumber
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PhoneNumberId = req.PhoneNumberId,
            WabaId = req.WabaId,
            DisplayName = req.DisplayName,
            AccessToken = req.AccessToken,
            VerifyToken = req.VerifyToken,
            Status = NumberStatus.Disconnected,
        };

        db.WhatsAppNumbers.Add(number);

        // Auto-create default BotConfig for this number
        db.BotConfigs.Add(new Domain.Entities.BotConfig
        {
            Id = Guid.NewGuid(),
            WhatsAppNumberId = number.Id,
            SystemPrompt = "Você é um atendente virtual prestativo. Responda sempre em português brasileiro de forma cordial e objetiva.",
            AiModel = "gpt-4o",
            Temperature = 0.3f,
            MaxTokens = 1000,
            IsActive = true,
            ContextMessages = 10,
            WorkStart = TimeOnly.Parse("08:00"),
            WorkEnd = TimeOnly.Parse("18:00"),
            OutOfHoursMsg = "Olá! Nosso atendimento é das 8h às 18h. Retornaremos em breve! 🕐",
        });

        await db.SaveChangesAsync(ct);

        return new WhatsAppNumberDto(
            number.Id, tenantId, number.PhoneNumberId, number.WabaId,
            number.DisplayName, number.Status, number.ConnectedAt);
    }
}

// ── UPDATE status ─────────────────────────────────────────────────
public class UpdateNumberStatusHandler(IOmniDbContext db)
    : IRequestHandler<UpdateNumberStatusCommand, bool>
{
    public async Task<bool> Handle(UpdateNumberStatusCommand req, CancellationToken ct)
    {
        var number = await db.WhatsAppNumbers.FindAsync([req.NumberId], ct);
        if (number is null) return false;

        number.Status = req.Status;
        if (req.Status == NumberStatus.Online)
            number.ConnectedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return true;
    }
}