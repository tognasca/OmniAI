using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class Message : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }

        public string? WaMessageId { get; set; }  // Meta API message ID (idempotency)

        public OmniAI.Domain.Enums.MessageDirection Direction { get; set; }
        public OmniAI.Domain.Enums.MessageType Type { get; set; }
        public OmniAI.Domain.Enums.SenderType SenderType { get; set; }

        public string? Content { get; set; }
        public string? MediaUrl { get; set; }
        public string? MediaMimeType { get; set; }

        public DateTime SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public DateTime? ReadAt { get; set; }

        public int? PromptTokens { get; set; }
        public int? CompletionTokens { get; set; }

        public Conversation Conversation { get; set; } = default!;
        public AiUsageLog? UsageLog { get; set; }
    }
}
