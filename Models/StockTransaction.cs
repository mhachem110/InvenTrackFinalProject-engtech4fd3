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

        public int InventoryItemID { get; set; }
        public InventoryItem InventoryItem { get; set; } = null!;

        public int? FromStorageLocationID { get; set; }
        public StorageLocation? FromStorageLocation { get; set; }

        public int? ToStorageLocationID { get; set; }
        public StorageLocation? ToStorageLocation { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (QuantityChange == 0)
                yield return new ValidationResult("Quantity change cannot be 0.", new[] { nameof(QuantityChange) });

            if (ActionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(Notes))
                yield return new ValidationResult("Notes are required for an Adjustment.", new[] { nameof(Notes) });

            if (ActionType == StockActionType.CheckIn)
            {
                if (!ToStorageLocationID.HasValue || ToStorageLocationID.Value < 1)
                    yield return new ValidationResult("Please select a location for Check In.", new[] { nameof(ToStorageLocationID) });
            }

            if (ActionType == StockActionType.CheckOut || ActionType == StockActionType.Adjustment)
            {
                if (!FromStorageLocationID.HasValue || FromStorageLocationID.Value < 1)
                    yield return new ValidationResult("Please select a location.", new[] { nameof(FromStorageLocationID) });
            }

            if (ActionType == StockActionType.Transfer)
            {
                if (!FromStorageLocationID.HasValue || FromStorageLocationID.Value < 1)
                    yield return new ValidationResult("Please select a source location.", new[] { nameof(FromStorageLocationID) });

                if (!ToStorageLocationID.HasValue || ToStorageLocationID.Value < 1)
                    yield return new ValidationResult("Please select a target location.", new[] { nameof(ToStorageLocationID) });

                if (FromStorageLocationID.HasValue && ToStorageLocationID.HasValue &&
                    FromStorageLocationID.Value == ToStorageLocationID.Value)
                    yield return new ValidationResult("Target location must be different from source location.", new[] { nameof(ToStorageLocationID) });
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