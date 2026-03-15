using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class InventoryItem
    {
        public InventoryItem()
        {
            StockTransactions = new HashSet<StockTransaction>();
            InventoryItemStocks = new HashSet<InventoryItemStock>();
        }

        public int ID { get; set; }

        [Display(Name = "Item Name")]
        [Required(ErrorMessage = "Item name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Item name must be between 2 and 120 characters.")]
        public string ItemName { get; set; } = string.Empty;

        [Display(Name = "SKU / Asset Tag")]
        [Required(ErrorMessage = "SKU / Asset Tag is required.")]
        [StringLength(50, ErrorMessage = "SKU / Asset Tag cannot exceed 50 characters.")]
        public string SKU { get; set; } = string.Empty;

        [Display(Name = "Description")]
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Display(Name = "Quantity On Hand")]
        [Range(0, int.MaxValue, ErrorMessage = "Quantity On Hand must be 0 or greater.")]
        public int QuantityOnHand { get; set; }

        [Display(Name = "Reorder Level")]
        [Range(0, int.MaxValue, ErrorMessage = "Reorder Level must be 0 or greater.")]
        public int ReorderLevel { get; set; }

        [Display(Name = "Is Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Category")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
        public int CategoryID { get; set; }
        public Category? Category { get; set; }

        [Display(Name = "Location")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a location.")]
        public int StorageLocationID { get; set; }
        public StorageLocation? StorageLocation { get; set; }

        public ICollection<StockTransaction> StockTransactions { get; set; }

        public ICollection<InventoryItemStock> InventoryItemStocks { get; set; }

        public ItemPhoto? ItemPhoto { get; set; }
        public ItemThumbnail? ItemThumbnail { get; set; }
    }
}