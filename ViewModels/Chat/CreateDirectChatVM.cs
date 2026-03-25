using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Chat
{
    public class CreateDirectChatVM
    {
        [Required]
        public string SelectedUserId { get; set; } = string.Empty;

        public List<UserPickerVM> Users { get; set; } = new();
    }
}
