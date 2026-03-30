using OmniAI.Domain.Enums;
namespace OmniAI.Domain.Entities;

public class Tenant : BaseEntity
{
    public Guid       Id       { get; set; }
    public string     Name     { get; set; } = default!;
    public string     Slug     { get; set; } = default!;
    public TenantPlan Plan     { get; set; }
    public bool       IsActive { get; set; } = true;

    public ICollection<WhatsAppNumber> Numbers  { get; } = new List<WhatsAppNumber>();
    public ICollection<Contact>        Contacts { get; } = new List<Contact>();
}
