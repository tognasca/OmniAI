namespace OmniAI.Domain.Interfaces;

public interface IHandoffService
{
    Task<bool> CheckTriggersAsync(Guid whatsAppNumberId, string message, CancellationToken ct = default);
    Task TriggerHandoffAsync(Guid conversationId, string reason, CancellationToken ct = default);
    Task AssignAgentAsync(Guid conversationId, Guid agentId, string agentName, CancellationToken ct = default);
}
