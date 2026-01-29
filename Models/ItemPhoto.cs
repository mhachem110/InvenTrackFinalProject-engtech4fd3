using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class ItemPhoto
    {
        public int ID { get; set; }

        [ScaffoldColumn(false)]
        public byte[] Content { get; set; }

        [StringLength(255)]
        public string MimeType { get; set; }

        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; }
    }
}
