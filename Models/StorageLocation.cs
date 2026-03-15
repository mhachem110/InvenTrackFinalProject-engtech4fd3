using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class StorageLocation
    {
        public StorageLocation()
        {
            InventoryItems = new HashSet<InventoryItem>();
            InventoryItemStocks = new HashSet<InventoryItemStock>();
            FromStockTransactions = new HashSet<StockTransaction>();
            ToStockTransactions = new HashSet<StockTransaction>();
            TransferRequestsFromHere = new HashSet<StockTransferRequest>();
            TransferRequestsToHere = new HashSet<StockTransferRequest>();
        }

        public int ID { get; set; }

        [Required(ErrorMessage = "Location name is required.")]
        [StringLength(120, MinimumLength = 2, ErrorMessage = "Location name must be between 2 and 120 characters.")]
        public string Name { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "Building cannot exceed 120 characters.")]
        public string? Building { get; set; }

        [StringLength(60, ErrorMessage = "Room cannot exceed 60 characters.")]
        public string? Room { get; set; }

        public ICollection<InventoryItem> InventoryItems { get; set; }
        public ICollection<InventoryItemStock> InventoryItemStocks { get; set; }

        public ICollection<StockTransaction> FromStockTransactions { get; set; }
        public ICollection<StockTransaction> ToStockTransactions { get; set; }

        public ICollection<StockTransferRequest> TransferRequestsFromHere { get; set; }
        public ICollection<StockTransferRequest> TransferRequestsToHere { get; set; }
    }
}