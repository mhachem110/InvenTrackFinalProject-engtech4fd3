using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class ChatConversationMember
    {
        public int ID { get; set; }

        [Required]
        public int ChatConversationID { get; set; }
        public ChatConversation ChatConversation { get; set; } = null!;

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string DisplayName { get; set; } = string.Empty;

        public bool IsAdmin { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastReadAt { get; set; }
        public DateTime? LeftAt { get; set; }

        public bool IsActive => LeftAt == null;
    }
}
