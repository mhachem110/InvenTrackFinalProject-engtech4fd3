using InvenTrack.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly InvenTrackContext _context;

        public ChatHub(InvenTrackContext context)
        {
            _context = context;
        }

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
                    // Join personal notification group
                    await Groups.AddToGroupAsync(Context.ConnectionId, GetUserNotificationsGroup(userId));

                    // Auto-join all active conversation groups so messages arrive instantly
                    // without relying on client-side JoinConversationGroup timing
                    var conversationIds = await _context.ChatConversationMembers
                        .AsNoTracking()
                        .Where(m => m.UserId == userId && m.LeftAt == null)
                        .Select(m => m.ChatConversationID)
                        .Distinct()
                        .ToListAsync();

                    foreach (var conversationId in conversationIds)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
                    }
                }
            }

            await base.OnConnectedAsync();
        }
    }
}
