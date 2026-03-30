using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmniAI.Domain.Entities
{
    public class Contact : BaseEntity
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }

        public string PhoneNumber { get; set; } = default!;
        public string? Name { get; set; }
        public string? ProfilePicUrl { get; set; }
        public string? Notes { get; set; }
        public bool IsBlocked { get; set; }

        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }

        // JSON column for custom tags/metadata
        public Dictionary<string, string> Tags { get; set; } = [];

        public Tenant Tenant { get; set; } = default!;
        public ICollection<Conversation> Conversations { get; } = [];
    }

}
