using System.ComponentModel.DataAnnotations;

namespace InvenTrack.Models
{
    public class StockTransaction : IValidatableObject
    {
        public int ID { get; set; }

        [Display(Name = "Reference #")]
        public int? ReferenceNumber { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.DateTime)]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [Display(Name = "Action")]
        public StockActionType ActionType { get; set; } = StockActionType.Adjustment;

        [Display(Name = "Quantity Change")]
        public int QuantityChange { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }

        [Display(Name = "Performed By")]
        [StringLength(120)]
        public string? PerformedBy { get; set; }

        // The item this transaction belongs to
        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;

        public int? FromStorageLocationID { get; set; }
        public StorageLocation? FromStorageLocation { get; set; }

        public int? ToStorageLocationID { get; set; }
        public StorageLocation? ToStorageLocation { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Quantity cannot be zero
            if (QuantityChange == 0)
            {
                yield return new ValidationResult(
                    "Quantity change cannot be 0.",
                    new[] { nameof(QuantityChange) }
                );
            }

            if ((ActionType == StockActionType.CheckIn || ActionType == StockActionType.CheckOut) && QuantityChange < 0)
            {
                yield return new ValidationResult(
                    "Quantity must be positive for Check In / Check Out.",
                    new[] { nameof(QuantityChange) }
                );
            }

            if (ActionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(Notes))
            {
                yield return new ValidationResult(
                    "Notes are required for an Adjustment.",
                    new[] { nameof(Notes) }
                );
            }

            // Transfer requires target location
            if (ActionType == StockActionType.Transfer && (!ToStorageLocationID.HasValue || ToStorageLocationID.Value < 1))
            {
                yield return new ValidationResult(
                    "Please select a target location for a transfer.",
                    new[] { nameof(ToStorageLocationID) }
                );
            }
        }
    }

    public enum StockActionType
    {
        CheckIn = 1,
        CheckOut = 2,
        Adjustment = 3,
        Transfer = 4
    }
}