using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class KnowledgeChunk
    {
        public Guid Id { get; set; }
        public Guid KnowledgeSourceId { get; set; }

        public string Content { get; set; } = default!;
        public int ChunkIndex { get; set; }
        public int TokenCount { get; set; }

        // Embedding stored as JSON (float[] with 1536 dims for text-embedding-3-large)
        // For PostgreSQL+pgvector, replace with: public Vector Embedding { get; set; }
        public string EmbeddingJson { get; set; } = "[]";

        [NotMapped]
        public float[] Embedding
        {
            get => JsonSerializer.Deserialize<float[]>(EmbeddingJson) ?? [];
            set => EmbeddingJson = JsonSerializer.Serialize(value);
        }

        public KnowledgeSource Source { get; set; } = default!;
    }
}
