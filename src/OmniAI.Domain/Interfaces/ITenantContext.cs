namespace OmniAI.Domain.Interfaces;

public interface ITenantContext
{
    Guid TenantId { get; }
}