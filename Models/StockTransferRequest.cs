using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class StockTransferRequest
    {
        public int ID { get; set; }

        [Required]
        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be 1 or more.")]
        public int Quantity { get; set; }

        [Required]
        public int FromStorageLocationID { get; set; }
        public StorageLocation FromStorageLocation { get; set; } = null!;

        [Required]
        public int ToStorageLocationID { get; set; }
        public StorageLocation ToStorageLocation { get; set; } = null!;

        [StringLength(500)]
        public string? Notes { get; set; }

        public TransferRequestStatus Status { get; set; } = TransferRequestStatus.Pending;

        [Required]
        [StringLength(450)]
        public string RequestedByUserId { get; set; } = string.Empty;

        [StringLength(256)]
        public string? RequestedByName { get; set; }

        public DateTime DateRequested { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? ReviewedByUserId { get; set; }

        [StringLength(256)]
        public string? ReviewedByName { get; set; }

        public DateTime? DateReviewed { get; set; }

        [StringLength(500)]
        public string? ReviewNotes { get; set; }
    }
}