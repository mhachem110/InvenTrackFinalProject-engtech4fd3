using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class InventoryItemStock
    {
        public int ID { get; set; }

        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;

        public int StorageLocationID { get; set; }
        public StorageLocation StorageLocation { get; set; } = null!;

        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }
    }
}