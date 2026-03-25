using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class ChatConversation
    {
        public ChatConversation()
        {
            Members = new HashSet<ChatConversationMember>();
            Messages = new HashSet<ChatMessage>();
        }

        public int ID { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        public bool IsGroupChat { get; set; }

        [Required]
        [StringLength(450)]
        public string CreatedByUserId { get; set; } = string.Empty;

        [StringLength(120)]
        public string CreatedByName { get; set; } = string.Empty;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;
        public DateTime? LastMessageAt { get; set; }

        public ICollection<ChatConversationMember> Members { get; set; }
        public ICollection<ChatMessage> Messages { get; set; }
    }
}
