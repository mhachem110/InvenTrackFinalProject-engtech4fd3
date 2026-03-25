using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class ChatMessage
    {
        public int ID { get; set; }

        [Required]
        public int ChatConversationID { get; set; }
        public ChatConversation ChatConversation { get; set; } = null!;

        [Required]
        [StringLength(450)]
        public string SenderUserId { get; set; } = string.Empty;

        [Required]
        [StringLength(120)]
        public string SenderDisplayName { get; set; } = string.Empty;

        [Required]
        [StringLength(4000)]
        public string Body { get; set; } = string.Empty;

        public DateTime DateSent { get; set; } = DateTime.UtcNow;
        public bool IsSystemMessage { get; set; }
    }
}
