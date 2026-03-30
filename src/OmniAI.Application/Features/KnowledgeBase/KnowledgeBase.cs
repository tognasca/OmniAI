using MediatR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application.Contracts.DTOs;
using OmniAI.Application.Contracts.Requests;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;
// ── KnowledgeBase ──────────────────────────────────────────────────
namespace OmniAI.Application.Features.KnowledgeBase;

public class GetKnowledgeSourcesHandler(IOmniDbContext db)
    : IRequestHandler<GetKnowledgeSourcesQuery, IEnumerable<KnowledgeSourceDto>>
{
    public async Task<IEnumerable<KnowledgeSourceDto>> Handle(
        GetKnowledgeSourcesQuery req, CancellationToken ct)
    {
        return await db.KnowledgeSources
            .Where(s => s.WhatsAppNumberId == req.WhatsAppNumberId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new KnowledgeSourceDto(
                s.Id, s.WhatsAppNumberId, s.SourceType,
                s.Title, s.TokenCount, s.Status, s.IndexedAt))
            .ToListAsync(ct);
    }
}

public class AddKnowledgeTextHandler(IOmniDbContext db, IBackgroundTaskQueue queue)
    : IRequestHandler<AddKnowledgeTextCommand, KnowledgeSourceDto>
{
    public async Task<KnowledgeSourceDto> Handle(AddKnowledgeTextCommand req, CancellationToken ct)
    {
        var source = new KnowledgeSource
        {
            Id = Guid.NewGuid(),
            WhatsAppNumberId = req.WhatsAppNumberId,
            SourceType = SourceType.ManualText,
            Title = req.Title,
            OriginalContent = req.Content,
            Status = SourceStatus.Pending,
        };
        db.KnowledgeSources.Add(source);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(source.Id, ct);
        return new KnowledgeSourceDto(source.Id, source.WhatsAppNumberId, source.SourceType, source.Title, 0, source.Status, null);
    }
}

public class AddKnowledgeUrlHandler(IOmniDbContext db, IBackgroundTaskQueue queue)
    : IRequestHandler<AddKnowledgeUrlCommand, KnowledgeSourceDto>
{
    public async Task<KnowledgeSourceDto> Handle(AddKnowledgeUrlCommand req, CancellationToken ct)
    {
        var source = new KnowledgeSource
        {
            Id = Guid.NewGuid(),
            WhatsAppNumberId = req.WhatsAppNumberId,
            SourceType = SourceType.UrlCrawl,
            Title = req.Title,
            OriginalUrl = req.Url,
            Status = SourceStatus.Pending,
        };
        db.KnowledgeSources.Add(source);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(source.Id, ct);
        return new KnowledgeSourceDto(source.Id, source.WhatsAppNumberId, source.SourceType, source.Title, 0, source.Status, null);
    }
}

public class DeleteKnowledgeSourceHandler(IOmniDbContext db)
    : IRequestHandler<DeleteKnowledgeSourceCommand, bool>
{
    public async Task<bool> Handle(DeleteKnowledgeSourceCommand req, CancellationToken ct)
    {
        var source = await db.KnowledgeSources.FindAsync([req.SourceId], ct);
        if (source is null) return false;
        source.IsDeleted = true;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

public class AddKnowledgeFileHandler(IOmniDbContext db, IBackgroundTaskQueue queue)
    : IRequestHandler<AddKnowledgeFileCommand, KnowledgeSourceDto>
{
    public async Task<KnowledgeSourceDto> Handle(AddKnowledgeFileCommand req, CancellationToken ct)
    {
        var source = new KnowledgeSource
        {
            Id = Guid.NewGuid(),
            WhatsAppNumberId = req.WhatsAppNumberId,
            SourceType = req.SourceType,
            Title = req.Title,
            FilePath = req.FilePath,
            Status = SourceStatus.Pending,
        };
        db.KnowledgeSources.Add(source);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(source.Id, ct);
        return new KnowledgeSourceDto(source.Id, source.WhatsAppNumberId, source.SourceType, source.Title, 0, source.Status, null);
    }
}