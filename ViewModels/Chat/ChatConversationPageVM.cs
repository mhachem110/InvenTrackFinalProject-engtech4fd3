using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Chat
{
    public class ChatConversationPageVM
    {
        public int ConversationId { get; set; }
        public string ConversationName { get; set; } = string.Empty;
        public bool IsGroupChat { get; set; }
        public List<ChatMemberVM> Members { get; set; } = new();
        public List<ChatMessageVM> Messages { get; set; } = new();
        public List<ChatConversationListItemVM> SidebarConversations { get; set; } = new();

        [Required]
        [StringLength(4000)]
        public string NewMessageBody { get; set; } = string.Empty;

        public bool CurrentUserCanManageMembers { get; set; }
        public int CurrentUserMemberCount => Members.Count;
        public List<UserPickerVM> AvailableUsersToAdd { get; set; } = new();
    }

    public class ChatMessageVM
    {
        public int MessageId { get; set; }
        public string SenderDisplayName { get; set; } = string.Empty;
        public string SenderUserId { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public DateTime DateSent { get; set; }
        public bool IsMine { get; set; }
        public bool IsSystemMessage { get; set; }
        public string DateSentDisplay => DateSent.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    public class ChatMemberVM
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }

    public class UserPickerVM
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
