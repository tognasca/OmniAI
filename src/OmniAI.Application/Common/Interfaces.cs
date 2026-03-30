using Microsoft.EntityFrameworkCore;
using OmniAI.Domain.Entities;
using OmniAI.Domain.Enums;

namespace OmniAI.Application;

// DB Context interface (keeps Application layer independent of EF)
public interface IOmniDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<WhatsAppNumber> WhatsAppNumbers { get; }
    DbSet<BotConfig> BotConfigs { get; }
    DbSet<Contact> Contacts { get; }
    DbSet<Conversation> Conversations { get; }
    DbSet<Message> Messages { get; }
    DbSet<KnowledgeSource> KnowledgeSources { get; }
    DbSet<KnowledgeChunk> KnowledgeChunks { get; }
    DbSet<HandoffTrigger> HandoffTriggers { get; }
    DbSet<AiUsageLog> AiUsageLogs { get; }
    DbSet<AppUser> AppUsers { get; }
    DbSet<RefreshToken> RefreshTokens { get; }   
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IDashboardNotifier
{
    Task NotifyConversationUpdatedAsync(Guid conversationId, ConversationStatus status);
    Task NotifyHandoffRequestedAsync(Guid conversationId, string contactName, string lastMessage);
    Task NotifyNewMessageAsync(Guid conversationId, string content, SenderType sender);
}

public interface IBackgroundTaskQueue
{
    Task EnqueueAsync(Guid knowledgeSourceId, CancellationToken ct = default);
    Task<Guid> DequeueAsync(CancellationToken ct);
}
