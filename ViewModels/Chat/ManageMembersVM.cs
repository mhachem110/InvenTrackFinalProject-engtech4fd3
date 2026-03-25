using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Chat
{
    public class ManageMembersVM
    {
        public int ConversationId { get; set; }
        public string ConversationName { get; set; } = string.Empty;
        public List<ChatMemberVM> Members { get; set; } = new();
        public List<UserPickerVM> AvailableUsers { get; set; } = new();

        [MinLength(1, ErrorMessage = "Select at least one user to add.")]
        public List<string> SelectedUserIds { get; set; } = new();
    }
}
