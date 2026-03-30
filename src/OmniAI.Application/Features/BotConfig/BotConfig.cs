using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
namespace OmniAI.Application.Features.BotConfig;

public class GetBotConfigHandler(IOmniDbContext db)
    : IRequestHandler<GetBotConfigQuery, BotConfigDto?>
{
    public async Task<BotConfigDto?> Handle(GetBotConfigQuery req, CancellationToken ct)
    {
        var c = await db.BotConfigs
            .FirstOrDefaultAsync(x => x.WhatsAppNumberId == req.WhatsAppNumberId, ct);
        return c is null ? null : Map(c);
    }

    internal static BotConfigDto Map(Domain.Entities.BotConfig c) =>
        new(c.Id, c.WhatsAppNumberId, c.SystemPrompt, c.AiModel,
            c.Temperature, c.MaxTokens, c.IsActive,
            c.WorkStart, c.WorkEnd, c.OutOfHoursMsg);
}

public class UpsertBotConfigHandler(IOmniDbContext db)
    : IRequestHandler<UpsertBotConfigCommand, BotConfigDto>
{
    public async Task<BotConfigDto> Handle(UpsertBotConfigCommand req, CancellationToken ct)
    {
        var config = await db.BotConfigs
            .FirstOrDefaultAsync(x => x.WhatsAppNumberId == req.WhatsAppNumberId, ct);

        if (config is null)
        {
            config = new Domain.Entities.BotConfig { Id = Guid.NewGuid(), WhatsAppNumberId = req.WhatsAppNumberId };
            db.BotConfigs.Add(config);
        }

        config.SystemPrompt = req.SystemPrompt;
        config.AiModel = req.AiModel;
        config.Temperature = req.Temperature;
        config.MaxTokens = req.MaxTokens;
        config.IsActive = req.IsActive;
        config.WorkStart = req.WorkStart;
        config.WorkEnd = req.WorkEnd;
        config.OutOfHoursMsg = req.OutOfHoursMsg;

        await db.SaveChangesAsync(ct);
        return GetBotConfigHandler.Map(config);
    }
}
