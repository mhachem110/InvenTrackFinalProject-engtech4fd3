using InvenTrack.Hubs;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.ViewModels.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles =
        AppRoles.Admin + "," +
        AppRoles.RegionalManager + "," +
        AppRoles.Manager + "," +
        AppRoles.Supervisor + "," +
        AppRoles.Employee)]
    public class ChatsController : Controller
    {
        private readonly ChatService _chatService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ChatHub> _chatHub;
        private readonly InvenTrack.Data.InvenTrackContext _context;

        public ChatsController(
            ChatService chatService,
            UserManager<ApplicationUser> userManager,
            IHubContext<ChatHub> chatHub,
            InvenTrack.Data.InvenTrackContext context)
        {
            _chatService = chatService;
            _userManager = userManager;
            _chatHub = chatHub;
            _context = context;
        }

        public async Task<IActionResult> Index(int? id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var sidebar = await _chatService.GetConversationListAsync(currentUser.Id);
            if (id == null)
            {
                if (sidebar.Any())
                    return RedirectToAction(nameof(Index), new { id = sidebar.First().ConversationId });

                ViewBag.EmptyState = true;
                ViewBag.Sidebar = sidebar;
                return View("Empty");
            }

            await _chatService.MarkConversationReadAsync(id.Value, currentUser.Id);
            var model = await _chatService.GetConversationPageAsync(id.Value, currentUser.Id);
            if (model == null) return NotFound();

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> NotificationSummary()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var summary = await _chatService.GetNotificationSummaryAsync(currentUser.Id);
            return Json(new
            {
                unreadConversationCount = summary.UnreadConversationCount,
                unreadMessageCount = summary.UnreadMessageCount
            });
        }

        [HttpPost]
        public async Task<IActionResult> MarkRead(int conversationId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            await _chatService.MarkConversationReadAsync(conversationId, currentUser.Id);
            return Ok(new { ok = true });
        }

        [HttpGet]
        public async Task<IActionResult> CreateDirect()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            return View(new CreateDirectChatVM
            {
                Users = await _chatService.GetAvailableUsersAsync(currentUser.Id)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDirect(CreateDirectChatVM vm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            vm.Users = await _chatService.GetAvailableUsersAsync(currentUser.Id);

            if (!ModelState.IsValid)
                return View(vm);

            var conversationId = await _chatService.CreateDirectConversationAsync(currentUser, vm.SelectedUserId);
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        [HttpGet]
        public async Task<IActionResult> CreateGroup()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            return View(new CreateGroupChatVM
            {
                Users = await _chatService.GetAvailableUsersAsync(currentUser.Id)
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGroup(CreateGroupChatVM vm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            vm.Users = await _chatService.GetAvailableUsersAsync(currentUser.Id);

            if (!ModelState.IsValid)
                return View(vm);

            var conversationId = await _chatService.CreateGroupConversationAsync(currentUser, vm.GroupName, vm.SelectedUserIds);
            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int conversationId, string body)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            if (string.IsNullOrWhiteSpace(body))
                return RedirectToAction(nameof(Index), new { id = conversationId });

            var message = await _chatService.SendMessageAsync(conversationId, currentUser, body);
            var recipientIds = await _context.ChatConversationMembers
                .AsNoTracking()
                .Where(m => m.ChatConversationID == conversationId && m.LeftAt == null && m.UserId != currentUser.Id)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();

            var conversationName = await _context.ChatConversations
                .AsNoTracking()
                .Where(c => c.ID == conversationId)
                .Select(c => c.Name)
                .FirstAsync();

            await _chatHub.Clients.Group($"chat-{conversationId}")
                .SendAsync("ReceiveMessage", new
                {
                    conversationId,
                    senderDisplayName = message.SenderDisplayName,
                    senderUserId = message.SenderUserId,
                    body = message.Body,
                    dateSent = message.DateSentDisplay,
                    isoDateSent = message.DateSent,
                    isMine = false,
                    isSystemMessage = false
                });

            foreach (var recipientId in recipientIds)
            {
                await _chatHub.Clients.Group(ChatHub.GetUserNotificationsGroup(recipientId))
                    .SendAsync("ChatNotification", new
                    {
                        conversationId,
                        conversationName,
                        senderDisplayName = message.SenderDisplayName,
                        preview = message.Body.Length > 120 ? message.Body[..120] + "..." : message.Body,
                        dateSent = message.DateSentDisplay,
                        isSystemMessage = false
                    });
            }

            return RedirectToAction(nameof(Index), new { id = conversationId });
        }

        [HttpGet]
        public async Task<IActionResult> ManageMembers(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var page = await _chatService.GetConversationPageAsync(id, currentUser.Id);
            if (page == null) return NotFound();
            if (!page.CurrentUserCanManageMembers) return Forbid();

            return View(new ManageMembersVM
            {
                ConversationId = page.ConversationId,
                ConversationName = page.ConversationName,
                Members = page.Members,
                AvailableUsers = page.AvailableUsersToAdd
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMembers(ManageMembersVM vm)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            await _chatService.AddMembersAsync(vm.ConversationId, currentUser, vm.SelectedUserIds);

            var addedUsers = await _userManager.Users
                .AsNoTracking()
                .Where(u => vm.SelectedUserIds.Contains(u.Id))
                .Select(u => new { u.Id, DisplayName = u.UserName ?? u.Email ?? u.Id })
                .ToListAsync();

            var conversationName = await _context.ChatConversations
                .AsNoTracking()
                .Where(c => c.ID == vm.ConversationId)
                .Select(c => c.Name)
                .FirstAsync();

            await _chatHub.Clients.Group($"chat-{vm.ConversationId}")
                .SendAsync("ConversationMembersChanged", new { conversationId = vm.ConversationId });

            foreach (var addedUser in addedUsers)
            {
                await _chatHub.Clients.Group(ChatHub.GetUserNotificationsGroup(addedUser.Id))
                    .SendAsync("ChatNotification", new
                    {
                        conversationId = vm.ConversationId,
                        conversationName,
                        senderDisplayName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                        preview = $"You were added to '{conversationName}'.",
                        dateSent = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                        isSystemMessage = true
                    });
            }

            return RedirectToAction(nameof(Index), new { id = vm.ConversationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveMember(int conversationId, string memberUserId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            await _chatService.RemoveMemberAsync(conversationId, currentUser, memberUserId);

            var conversationName = await _context.ChatConversations
                .AsNoTracking()
                .Where(c => c.ID == conversationId)
                .Select(c => c.Name)
                .FirstAsync();

            await _chatHub.Clients.Group($"chat-{conversationId}")
                .SendAsync("ConversationMembersChanged", new { conversationId });

            await _chatHub.Clients.Group(ChatHub.GetUserNotificationsGroup(memberUserId))
                .SendAsync("ChatNotification", new
                {
                    conversationId,
                    conversationName,
                    senderDisplayName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                    preview = $"You were removed from '{conversationName}'.",
                    dateSent = DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                    isSystemMessage = true
                });

            return RedirectToAction(nameof(ManageMembers), new { id = conversationId });
        }
    }
}
