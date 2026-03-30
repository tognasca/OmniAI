using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class BotConfig : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid WhatsAppNumberId { get; set; }

        public string SystemPrompt { get; set; } = default!;
        public string AiModel { get; set; } = "gpt-4o";
        public float Temperature { get; set; } = 0.3f;
        public int MaxTokens { get; set; } = 1000;

        public bool IsActive { get; set; } = true;
        public bool ReplyGroups { get; set; }
        public bool EnableTts { get; set; }
        public int ContextMessages { get; set; } = 10;

        public TimeOnly? WorkStart { get; set; }
        public TimeOnly? WorkEnd { get; set; }
        public string? OutOfHoursMsg { get; set; }

        public WhatsAppNumber Number { get; set; } = default!;
    }
}
