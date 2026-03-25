using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Chat
{
    public class CreateGroupChatVM
    {
        [Required]
        [StringLength(120)]
        public string GroupName { get; set; } = string.Empty;

        [MinLength(1, ErrorMessage = "Select at least one other user.")]
        public List<string> SelectedUserIds { get; set; } = new();

        public List<UserPickerVM> Users { get; set; } = new();
    }
}
