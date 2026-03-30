namespace OmniAI.Domain.Interfaces;

public interface IWhatsAppService
{
    Task SendTextAsync(Guid numberId, string to, string text, CancellationToken ct = default);
    Task SendTemplateAsync(Guid numberId, string to, string templateName, object[] components, CancellationToken ct = default);
    Task MarkAsReadAsync(Guid numberId, string waMessageId, CancellationToken ct = default);
}