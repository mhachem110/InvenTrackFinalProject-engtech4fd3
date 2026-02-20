using System;
using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class ItemThumbnail
    {
        public int ID { get; set; }

        [ScaffoldColumn(false)]
        [Required]
        public byte[] Content { get; set; } = Array.Empty<byte>();

        [StringLength(255)]
        [Required]
        public string MimeType { get; set; } = "image/webp";

        public int InventoryItemID { get; set; }
        public InventoryItem? InventoryItem { get; set; }
    }
}