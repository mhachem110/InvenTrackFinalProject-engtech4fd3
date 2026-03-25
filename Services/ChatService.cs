using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.ViewModels.Chat;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Services
{
    public class ChatService
    {
        private readonly InvenTrackContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatService(InvenTrackContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<List<ChatConversationListItemVM>> GetConversationListAsync(string userId)
        {
            var conversations = await _context.ChatConversations
                .AsNoTracking()
                .Where(c => c.Members.Any(m => m.UserId == userId && m.LeftAt == null))
                .Select(c => new ChatConversationListItemVM
                {
                    ConversationId = c.ID,
                    Name = c.Name,
                    IsGroupChat = c.IsGroupChat,
                    MemberCount = c.Members.Count(m => m.LeftAt == null),
                    LastMessagePreview = c.Messages
                        .OrderByDescending(m => m.DateSent)
                        .Select(m => m.Body)
                        .FirstOrDefault() ?? "No messages yet.",
                    LastMessageAt = c.Messages
                        .OrderByDescending(m => m.DateSent)
                        .Select(m => (DateTime?)m.DateSent)
                        .FirstOrDefault(),
                    UnreadCount = c.Messages.Count(m =>
                        !m.IsSystemMessage &&
                        m.SenderUserId != userId &&
                        m.DateSent > (
                            c.Members
                                .Where(cm => cm.UserId == userId && cm.LeftAt == null)
                                .Select(cm => cm.LastReadAt ?? cm.JoinedAt)
                                .FirstOrDefault()))
                })
                .OrderByDescending(x => x.LastMessageAt ?? DateTime.MinValue)
                .ThenBy(x => x.Name)
                .ToListAsync();

            return conversations;
        }

        public async Task<ChatConversationPageVM?> GetConversationPageAsync(int conversationId, string userId)
        {
            var convo = await _context.ChatConversations
                .AsNoTracking()
                .Include(c => c.Members)
                .Include(c => c.Messages.OrderBy(m => m.DateSent))
                .FirstOrDefaultAsync(c => c.ID == conversationId && c.Members.Any(m => m.UserId == userId && m.LeftAt == null));

            if (convo == null)
                return null;

            var page = new ChatConversationPageVM
            {
                ConversationId = convo.ID,
                ConversationName = convo.Name,
                IsGroupChat = convo.IsGroupChat,
                SidebarConversations = await GetConversationListAsync(userId),
                Members = convo.Members
                    .Where(m => m.LeftAt == null)
                    .OrderByDescending(m => m.IsAdmin)
                    .ThenBy(m => m.DisplayName)
                    .Select(m => new ChatMemberVM
                    {
                        UserId = m.UserId,
                        DisplayName = m.DisplayName,
                        IsAdmin = m.IsAdmin
                    }).ToList(),
                Messages = convo.Messages
                    .OrderBy(m => m.DateSent)
                    .Select(m => new ChatMessageVM
                    {
                        MessageId = m.ID,
                        SenderUserId = m.SenderUserId,
                        SenderDisplayName = m.SenderDisplayName,
                        Body = m.Body,
                        DateSent = m.DateSent,
                        IsMine = m.SenderUserId == userId,
                        IsSystemMessage = m.IsSystemMessage
                    }).ToList(),
                CurrentUserCanManageMembers = convo.IsGroupChat && convo.Members.Any(m => m.UserId == userId && m.LeftAt == null && m.IsAdmin)
            };

            if (page.CurrentUserCanManageMembers)
            {
                var memberIds = page.Members.Select(x => x.UserId).ToHashSet();
                page.AvailableUsersToAdd = await _userManager.Users
                    .AsNoTracking()
                    .Where(u => !memberIds.Contains(u.Id))
                    .OrderBy(u => u.UserName)
                    .Select(u => new UserPickerVM
                    {
                        UserId = u.Id,
                        DisplayName = u.UserName ?? u.Email ?? u.Id,
                        Email = u.Email ?? string.Empty
                    })
                    .ToListAsync();
            }

            return page;
        }

        public async Task<List<UserPickerVM>> GetAvailableUsersAsync(string currentUserId)
        {
            return await _userManager.Users
                .AsNoTracking()
                .Where(u => u.Id != currentUserId)
                .OrderBy(u => u.UserName)
                .Select(u => new UserPickerVM
                {
                    UserId = u.Id,
                    DisplayName = u.UserName ?? u.Email ?? u.Id,
                    Email = u.Email ?? string.Empty
                })
                .ToListAsync();
        }

        public async Task<int> CreateDirectConversationAsync(ApplicationUser currentUser, string otherUserId)
        {
            var otherUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == otherUserId);
            if (otherUser == null)
                throw new InvalidOperationException("Selected user was not found.");

            var existingConversationId = await _context.ChatConversations
                .Where(c => !c.IsGroupChat)
                .Where(c => c.Members.Count(m => m.LeftAt == null) == 2)
                .Where(c => c.Members.Any(m => m.UserId == currentUser.Id && m.LeftAt == null))
                .Where(c => c.Members.Any(m => m.UserId == otherUser.Id && m.LeftAt == null))
                .Select(c => c.ID)
                .FirstOrDefaultAsync();

            if (existingConversationId > 0)
                return existingConversationId;

            var convo = new ChatConversation
            {
                Name = otherUser.UserName ?? otherUser.Email ?? "Direct Chat",
                IsGroupChat = false,
                CreatedByUserId = currentUser.Id,
                CreatedByName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                DateCreated = DateTime.UtcNow,
                LastMessageAt = DateTime.UtcNow
            };

            _context.ChatConversations.Add(convo);
            await _context.SaveChangesAsync();

            _context.ChatConversationMembers.AddRange(
                new ChatConversationMember
                {
                    ChatConversationID = convo.ID,
                    UserId = currentUser.Id,
                    DisplayName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                    IsAdmin = true,
                    LastReadAt = DateTime.UtcNow
                },
                new ChatConversationMember
                {
                    ChatConversationID = convo.ID,
                    UserId = otherUser.Id,
                    DisplayName = otherUser.UserName ?? otherUser.Email ?? "Unknown",
                    IsAdmin = true
                });

            await _context.SaveChangesAsync();
            return convo.ID;
        }

        public async Task<int> CreateGroupConversationAsync(ApplicationUser currentUser, string groupName, IEnumerable<string> memberUserIds)
        {
            var uniqueIds = memberUserIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => x != currentUser.Id)
                .ToList();

            if (!uniqueIds.Any())
                throw new InvalidOperationException("Select at least one user.");

            var users = await _userManager.Users
                .Where(u => uniqueIds.Contains(u.Id))
                .ToListAsync();

            var now = DateTime.UtcNow;

            var convo = new ChatConversation
            {
                Name = groupName.Trim(),
                IsGroupChat = true,
                CreatedByUserId = currentUser.Id,
                CreatedByName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                DateCreated = now,
                LastMessageAt = now
            };

            _context.ChatConversations.Add(convo);
            await _context.SaveChangesAsync();

            _context.ChatConversationMembers.Add(new ChatConversationMember
            {
                ChatConversationID = convo.ID,
                UserId = currentUser.Id,
                DisplayName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                IsAdmin = true,
                LastReadAt = now
            });

            foreach (var user in users)
            {
                _context.ChatConversationMembers.Add(new ChatConversationMember
                {
                    ChatConversationID = convo.ID,
                    UserId = user.Id,
                    DisplayName = user.UserName ?? user.Email ?? "Unknown",
                    IsAdmin = false
                });
            }

            _context.ChatMessages.Add(new ChatMessage
            {
                ChatConversationID = convo.ID,
                SenderUserId = currentUser.Id,
                SenderDisplayName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                Body = $"Group chat '{convo.Name}' created.",
                IsSystemMessage = true,
                DateSent = now
            });

            await _context.SaveChangesAsync();
            return convo.ID;
        }

        public async Task<ChatMessageVM> SendMessageAsync(int conversationId, ApplicationUser sender, string body)
        {
            var now = DateTime.UtcNow;

            var membership = await _context.ChatConversationMembers
                .FirstOrDefaultAsync(m => m.ChatConversationID == conversationId && m.UserId == sender.Id && m.LeftAt == null);

            if (membership == null)
                throw new InvalidOperationException("You are not a member of this conversation.");

            var message = new ChatMessage
            {
                ChatConversationID = conversationId,
                SenderUserId = sender.Id,
                SenderDisplayName = sender.UserName ?? sender.Email ?? "Unknown",
                Body = body.Trim(),
                DateSent = now
            };

            _context.ChatMessages.Add(message);

            membership.LastReadAt = now;

            var convo = await _context.ChatConversations.FirstAsync(c => c.ID == conversationId);
            convo.LastMessageAt = now;

            await _context.SaveChangesAsync();

            return new ChatMessageVM
            {
                MessageId = message.ID,
                SenderUserId = message.SenderUserId,
                SenderDisplayName = message.SenderDisplayName,
                Body = message.Body,
                DateSent = message.DateSent,
                IsMine = true,
                IsSystemMessage = false
            };
        }

        public async Task AddMembersAsync(int conversationId, ApplicationUser actingUser, IEnumerable<string> userIds)
        {
            var convo = await _context.ChatConversations
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.ID == conversationId);

            if (convo == null)
                throw new InvalidOperationException("Conversation not found.");

            var actingMembership = convo.Members.FirstOrDefault(m => m.UserId == actingUser.Id && m.LeftAt == null);
            if (actingMembership == null || !actingMembership.IsAdmin || !convo.IsGroupChat)
                throw new InvalidOperationException("You cannot manage members for this conversation.");

            var now = DateTime.UtcNow;
            var newIds = userIds
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(x => !convo.Members.Any(m => m.UserId == x && m.LeftAt == null))
                .ToList();

            var users = await _userManager.Users.Where(u => newIds.Contains(u.Id)).ToListAsync();

            foreach (var user in users)
            {
                _context.ChatConversationMembers.Add(new ChatConversationMember
                {
                    ChatConversationID = conversationId,
                    UserId = user.Id,
                    DisplayName = user.UserName ?? user.Email ?? "Unknown",
                    IsAdmin = false
                });

                _context.ChatMessages.Add(new ChatMessage
                {
                    ChatConversationID = conversationId,
                    SenderUserId = actingUser.Id,
                    SenderDisplayName = actingUser.UserName ?? actingUser.Email ?? "Unknown",
                    Body = $"{user.UserName ?? user.Email ?? "User"} joined the group.",
                    IsSystemMessage = true,
                    DateSent = now
                });
            }

            convo.LastMessageAt = now;
            actingMembership.LastReadAt = now;

            await _context.SaveChangesAsync();
        }

        public async Task RemoveMemberAsync(int conversationId, ApplicationUser actingUser, string memberUserId)
        {
            var convo = await _context.ChatConversations
                .Include(c => c.Members)
                .FirstOrDefaultAsync(c => c.ID == conversationId);

            if (convo == null)
                throw new InvalidOperationException("Conversation not found.");

            var actingMembership = convo.Members.FirstOrDefault(m => m.UserId == actingUser.Id && m.LeftAt == null);
            if (actingMembership == null || !actingMembership.IsAdmin || !convo.IsGroupChat)
                throw new InvalidOperationException("You cannot manage members for this conversation.");

            var membership = convo.Members.FirstOrDefault(m => m.UserId == memberUserId && m.LeftAt == null);
            if (membership == null)
                throw new InvalidOperationException("Member not found.");

            var now = DateTime.UtcNow;
            membership.LeftAt = now;

            _context.ChatMessages.Add(new ChatMessage
            {
                ChatConversationID = conversationId,
                SenderUserId = actingUser.Id,
                SenderDisplayName = actingUser.UserName ?? actingUser.Email ?? "Unknown",
                Body = $"{membership.DisplayName} was removed from the group.",
                IsSystemMessage = true,
                DateSent = now
            });

            convo.LastMessageAt = now;
            actingMembership.LastReadAt = now;

            await _context.SaveChangesAsync();
        }

        public async Task MarkConversationReadAsync(int conversationId, string userId)
        {
            var membership = await _context.ChatConversationMembers
                .FirstOrDefaultAsync(m => m.ChatConversationID == conversationId && m.UserId == userId && m.LeftAt == null);

            if (membership == null)
                return;

            membership.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<ChatNotificationSummaryVM> GetNotificationSummaryAsync(string userId)
        {
            var memberships = await _context.ChatConversationMembers
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.LeftAt == null)
                .Select(m => new
                {
                    m.ChatConversationID,
                    LastSeen = m.LastReadAt ?? m.JoinedAt
                })
                .ToListAsync();

            if (!memberships.Any())
            {
                return new ChatNotificationSummaryVM();
            }

            var conversationIds = memberships.Select(m => m.ChatConversationID).ToList();
            var lastSeenByConversation = memberships.ToDictionary(x => x.ChatConversationID, x => x.LastSeen);

            var unreadMessages = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => conversationIds.Contains(m.ChatConversationID))
                .Where(m => !m.IsSystemMessage)
                .Where(m => m.SenderUserId != userId)
                .Select(m => new { m.ChatConversationID, m.DateSent })
                .ToListAsync();

            var unreadConversations = unreadMessages
                .GroupBy(m => m.ChatConversationID)
                .Count(g => g.Any(x => x.DateSent > lastSeenByConversation[g.Key]));

            var unreadCount = unreadMessages
                .Count(m => m.DateSent > lastSeenByConversation[m.ChatConversationID]);

            return new ChatNotificationSummaryVM
            {
                UnreadConversationCount = unreadConversations,
                UnreadMessageCount = unreadCount
            };
        }
    }
}
