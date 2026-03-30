using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class AiUsageLog
    {
        public Guid Id { get; set; }
        public Guid MessageId { get; set; }
        public Guid WhatsAppNumberId { get; set; }

        public string Model { get; set; } = default!;
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal CostUsd { get; set; }
        public bool RagUsed { get; set; }
        public int RagChunksUsed { get; set; }

        public DateTime CreatedAt { get; set; }

        public Message Message { get; set; } = default!;
    }
}
