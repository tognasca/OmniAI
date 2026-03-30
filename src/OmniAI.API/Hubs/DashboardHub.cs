using Microsoft.AspNetCore.SignalR;
using OmniAI.Application;
using OmniAI.Domain.Enums;

namespace OmniAI.API.Hubs;

public class DashboardHub : Hub
{
    public async Task JoinTenantGroup(string tenantId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant-{tenantId}");
}

public class SignalRDashboardNotifier(IHubContext<DashboardHub> hub) : IDashboardNotifier
{
    public Task NotifyConversationUpdatedAsync(Guid id, ConversationStatus status)
        => hub.Clients.All.SendAsync("ConversationUpdated", new { id, status = status.ToString(), ts = DateTime.UtcNow });
    public Task NotifyHandoffRequestedAsync(Guid id, string contactName, string lastMessage)
        => hub.Clients.All.SendAsync("HandoffRequested", new { id, contactName, lastMessage, ts = DateTime.UtcNow });
    public Task NotifyNewMessageAsync(Guid id, string content, SenderType sender)
        => hub.Clients.All.SendAsync("NewMessage", new { id, content, sender = sender.ToString(), ts = DateTime.UtcNow });
}
