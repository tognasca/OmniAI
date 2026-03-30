using OmniAI.Domain.Enums;
namespace OmniAI.Domain.Entities;

public class WhatsAppNumber : BaseEntity
{
    public Guid         Id            { get; set; }
    public Guid         TenantId      { get; set; }
    public string       PhoneNumberId { get; set; } = default!;
    public string       WabaId        { get; set; } = default!;
    public string       DisplayName   { get; set; } = default!;
    public string       AccessToken   { get; set; } = default!;
    public string       VerifyToken   { get; set; } = default!;
    public NumberStatus Status        { get; set; }
    public DateTime?    ConnectedAt   { get; set; }

    public Tenant                        Tenant        { get; set; } = default!;
    public BotConfig?                    Config        { get; set; }
    public ICollection<Conversation>     Conversations { get; } = new List<Conversation>();
    public ICollection<KnowledgeSource>  Sources       { get; } = new List<KnowledgeSource>();
    public ICollection<HandoffTrigger>   Triggers      { get; } = new List<HandoffTrigger>();
    public ICollection<AiUsageLog>       UsageLogs     { get; } = new List<AiUsageLog>();
}
