using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class InventoryOrderRequest
    {
        public int ID { get; set; }

        [Required]
        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;

        [Required]
        public int DestinationStorageLocationID { get; set; }
        public StorageLocation DestinationStorageLocation { get; set; } = null!;

        [StringLength(500)]
        public string? RelatedLocationIdsCsv { get; set; }

        [StringLength(500)]
        public string? RelatedLocationNames { get; set; }

        public int CurrentVisibleQuantity { get; set; }
        public int SuggestedQuantity { get; set; }
        public int RequestedQuantity { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required, StringLength(450)]
        public string RequestedByUserId { get; set; } = string.Empty;

        [Required, StringLength(120)]
        public string RequestedByName { get; set; } = string.Empty;

        public DateTime DateRequested { get; set; } = DateTime.UtcNow;

        public OrderRequestStatus Status { get; set; } = OrderRequestStatus.Pending;

        [StringLength(450)]
        public string? ReviewedByUserId { get; set; }

        [StringLength(120)]
        public string? ReviewedByName { get; set; }

        public DateTime? DateReviewed { get; set; }

        [StringLength(200)]
        public string? ReviewDecision { get; set; }

        [StringLength(120)]
        public string? FulfilledBy { get; set; }

        public DateTime? DateFulfilled { get; set; }

        public int? StockTransactionID { get; set; }
        public StockTransaction? StockTransaction { get; set; }

        public bool RequiresApproval { get; set; }
    }
}
