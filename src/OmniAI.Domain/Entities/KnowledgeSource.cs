using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class KnowledgeSource : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid WhatsAppNumberId { get; set; }

        public OmniAI.Domain.Enums.SourceType SourceType { get; set; }
        public string Title { get; set; } = default!;
        public string? OriginalUrl { get; set; }
        public string? FilePath { get; set; }
        public string? OriginalContent { get; set; }
        public int TokenCount { get; set; }

        public OmniAI.Domain.Enums.SourceStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? IndexedAt { get; set; }

        public WhatsAppNumber Number { get; set; } = default!;
        public ICollection<KnowledgeChunk> Chunks { get; } = [];
    }
}
