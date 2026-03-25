namespace InvenTrack.ViewModels.Chat
{
    public class ChatConversationListItemVM
    {
        public int ConversationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsGroupChat { get; set; }
        public int MemberCount { get; set; }
        public string LastMessagePreview { get; set; } = "No messages yet.";
        public DateTime? LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
    }
}
