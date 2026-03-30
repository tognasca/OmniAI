using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmniAI.Application;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;
using OmniAI.Domain.Interfaces;

namespace OmniAI.Infrastructure.Services.KnowledgeIndexing;

/// <summary>
/// Chunks a KnowledgeSource into ~500-token pieces, generates embeddings,
/// persists KnowledgeChunk rows, and performs cosine-similarity RAG search.
/// </summary>
public class KnowledgeIndexingService(
    IOmniDbContext db,
    IAiService ai,
    ILogger<KnowledgeIndexingService> log) : IKnowledgeIndexingService
{
    private const int ChunkSize = 500;   // tokens per chunk
    private const int ChunkOverlap = 50; // overlap for context continuity

    public async Task IndexSourceAsync(Guid knowledgeSourceId, CancellationToken ct = default)
    {
        var source = await db.KnowledgeSources
            .Include(s => s.Chunks)
            .FirstOrDefaultAsync(s => s.Id == knowledgeSourceId, ct);

        if (source is null)
        {
            log.LogWarning("KnowledgeSource {Id} not found", knowledgeSourceId);
            return;
        }

        source.Status = SourceStatus.Processing;
        await db.SaveChangesAsync(ct);

        try
        {
            // 1. Fetch content (crawl URL if needed)
            var content = source.SourceType == SourceType.UrlCrawl
                ? await CrawlUrlAsync(source.OriginalUrl!, ct)
                : source.OriginalContent ?? "";

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("No content to index");

            source.OriginalContent = content;

            // 2. Remove old chunks
            db.KnowledgeChunks.RemoveRange(source.Chunks);

            // 3. Split into chunks
            var chunks = SplitIntoChunks(content, ChunkSize, ChunkOverlap);

            // 4. Generate embeddings and persist
            int totalTokens = 0;
            for (int i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var chunkText = chunks[i];
                var embedding = await ai.GetEmbeddingAsync(chunkText, ct);
                var tokenCount = EstimateTokens(chunkText);
                totalTokens += tokenCount;

                var chunk = new KnowledgeChunk
                {
                    Id = Guid.NewGuid(),
                    KnowledgeSourceId = source.Id,
                    Content = chunkText,
                    ChunkIndex = i,
                    TokenCount = tokenCount,
                    Embedding = embedding
                };

                db.KnowledgeChunks.Add(chunk);
                log.LogDebug("Indexed chunk {I}/{Total} for source {SourceId}", i + 1, chunks.Count, source.Id);
            }

            source.TokenCount = totalTokens;
            source.Status = SourceStatus.Indexed;
            source.IndexedAt = DateTime.UtcNow;
            source.ErrorMessage = null;

            await db.SaveChangesAsync(ct);
            log.LogInformation("Indexed {Chunks} chunks ({Tokens} tokens) for source {SourceId}",
                chunks.Count, totalTokens, source.Id);
        }
        catch (Exception ex)
        {
            source.Status = SourceStatus.Failed;
            source.ErrorMessage = ex.Message[..Math.Min(ex.Message.Length, 1999)];
            await db.SaveChangesAsync(ct);
            log.LogError(ex, "Failed to index source {SourceId}", source.Id);
        }
    }

    public async Task<IEnumerable<string>> SearchAsync(
        Guid whatsAppNumberId, string query, int topK = 5, CancellationToken ct = default)
    {
        // 1. Embed query
        var queryEmbedding = await ai.GetEmbeddingAsync(query, ct);

        // 2. Load all indexed chunks for this number
        var chunks = await db.KnowledgeChunks
            .Where(c => c.Source.WhatsAppNumberId == whatsAppNumberId
                     && c.Source.Status == SourceStatus.Indexed)
            .Select(c => new { c.Content, c.EmbeddingJson })
            .ToListAsync(ct);

        if (chunks.Count == 0) return [];

        // 3. Cosine similarity in memory (for SQL Server; use pgvector for large scale)
        var scored = chunks
            .Select(c => new
            {
                c.Content,
                Score = CosineSimilarity(queryEmbedding,
                    System.Text.Json.JsonSerializer.Deserialize<float[]>(c.EmbeddingJson) ?? [])
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Where(x => x.Score > 0.7f) // relevance threshold
            .Select(x => x.Content)
            .ToList();

        return scored;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string> SplitIntoChunks(string text, int chunkSize, int overlap)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();

        int i = 0;
        while (i < words.Length)
        {
            var end = Math.Min(i + chunkSize, words.Length);
            chunks.Add(string.Join(" ", words[i..end]));
            i += chunkSize - overlap;
        }

        return chunks;
    }

    private static int EstimateTokens(string text) =>
        (int)Math.Ceiling(text.Length / 4.0); // ~4 chars per token

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;

        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA == 0 || normB == 0) return 0f;
        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private static async Task<string> CrawlUrlAsync(string url, CancellationToken ct)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("User-Agent", "OmniAI-Crawler/1.0");
        var html = await client.GetStringAsync(url, ct);

        // Basic HTML stripping (HtmlAgilityPack)
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        // Remove scripts, styles, nav elements
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
                node.Remove();
        }

        return doc.DocumentNode.InnerText
            .Replace("\t", " ")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Trim();
    }
}
