using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class HandoffTrigger : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid WhatsAppNumberId { get; set; }

        public OmniAI.Domain.Enums.TriggerType TriggerType { get; set; }
        public string Value { get; set; } = default!;
        public OmniAI.Domain.Enums.TriggerPriority Priority { get; set; }
        public bool IsActive { get; set; } = true;
        public string? TransitionMsg { get; set; }

        public WhatsAppNumber Number { get; set; } = default!;
    }
}
