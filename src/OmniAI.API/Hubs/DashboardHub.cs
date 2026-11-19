using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OmniAI.Application;
using OmniAI.Domain.Enums;

namespace OmniAI.API.Hubs;

public class DashboardHub(IOmniDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst("tenantId")?.Value, out var tenantId) ||
            !await db.Tenants.AnyAsync(t => t.Id == tenantId && t.IsActive, Context.ConnectionAborted))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, Group(tenantId), Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    internal static string Group(Guid tenantId) => $"tenant-{tenantId:D}";
}

public class SignalRDashboardNotifier(IHubContext<DashboardHub> hub, IOmniDbContext db) : IDashboardNotifier
{
    public Task NotifyConversationUpdatedAsync(Guid id, ConversationStatus status)
        => PublishAsync(id, "ConversationUpdated", new { conversationId = id, status = status.ToString(), timestamp = DateTime.UtcNow });

    public Task NotifyHandoffRequestedAsync(Guid id, string contactName, string lastMessage)
        => PublishAsync(id, "HandoffRequested", new { conversationId = id, contactName, lastMessage, timestamp = DateTime.UtcNow });

    public Task NotifyNewMessageAsync(Guid id, string content, SenderType sender)
        => PublishAsync(id, "NewMessage", new { conversationId = id, content, sender = sender.ToString(), timestamp = DateTime.UtcNow });

    private async Task PublishAsync(Guid conversationId, string eventName, object payload)
    {
        var tenantId = await db.Conversations
            .Where(c => c.Id == conversationId && c.Number.Tenant.IsActive)
            .Select(c => (Guid?)c.Number.TenantId)
            .SingleOrDefaultAsync();

        if (tenantId is { } id)
            await hub.Clients.Group(DashboardHub.Group(id)).SendAsync(eventName, payload);
    }
}
