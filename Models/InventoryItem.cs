using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class InventoryItem
    {
        public InventoryItem()
        {
            this.StockTransactions = new HashSet<StockTransaction>();
        }

        public int ID { get; set; }

        [Display(Name = "Item Name")]
        [Required]
        [StringLength(120)]
        public string ItemName { get; set; }

        [Display(Name = "SKU / Asset Tag")]
        [StringLength(50)]
        public string SKU { get; set; }

        [Display(Name = "Description")]
        [StringLength(500)]
        public string Description { get; set; }

        [Display(Name = "Quantity On Hand")]
        [Range(0, int.MaxValue)]
        public int QuantityOnHand { get; set; }

        [Display(Name = "Reorder Level")]
        [Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Category")]
        public int? CategoryID { get; set; }
        public Category Category { get; set; }

        [Display(Name = "Location")]
        public int? StorageLocationID { get; set; }
        public StorageLocation StorageLocation { get; set; }

        public ICollection<StockTransaction> StockTransactions { get; set; }

        // Optional: reuse existing image pattern (full photo + thumbnail)
        public ItemPhoto ItemPhoto { get; set; }
        public ItemThumbnail ItemThumbnail { get; set; }
    }
}
