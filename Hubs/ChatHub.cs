using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace InvenTrack.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        public static string GetConversationGroup(int conversationId) => $"chat-{conversationId}";
        public static string GetUserNotificationsGroup(string userId) => $"chat-user-{userId}";

        public Task JoinConversationGroup(string conversationId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroup(int.Parse(conversationId)));
        }

        public Task LeaveConversationGroup(string conversationId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroup(int.Parse(conversationId)));
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var userId = Context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, GetUserNotificationsGroup(userId));
                }
            }

            await base.OnConnectedAsync();
        }
    }
}
