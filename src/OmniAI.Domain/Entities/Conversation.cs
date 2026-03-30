using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class Conversation : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid ContactId { get; set; }
        public Guid WhatsAppNumberId { get; set; }

        public OmniAI.Domain.Enums.ConversationStatus Status { get; set; }
        public OmniAI.Domain.Enums.HandoffReason HandoffReason { get; set; }

        public Guid? AssignedAgentId { get; set; }
        public string? AgentName { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        public int? CsatScore { get; set; }
        public string? CsatComment { get; set; }

        public Contact Contact { get; set; } = default!;
        public WhatsAppNumber Number { get; set; } = default!;
        public ICollection<Message> Messages { get; } = [];
    }

}
