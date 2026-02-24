using System.ComponentModel.DataAnnotations;
using InvenTrack.Models;

namespace InvenTrack.ViewModels
{
    public class StockTransactionCreateVM : IValidatableObject
    {
        public int InventoryItemID { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;

        public int TotalQuantityOnHand { get; set; }

        public StockActionType ActionType { get; set; } = StockActionType.CheckIn;

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [Display(Name = "From Location")]
        public int? FromLocationID { get; set; }

        [Display(Name = "To Location")]
        public int? ToLocationID { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }

        [Display(Name = "Performed By")]
        [StringLength(120)]
        public string? PerformedBy { get; set; }

        public List<LocationStockVM> Locations { get; set; } = new();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Quantity == 0)
                yield return new ValidationResult("Quantity cannot be 0.", new[] { nameof(Quantity) });

            if (ActionType == StockActionType.CheckIn || ActionType == StockActionType.CheckOut || ActionType == StockActionType.Transfer)
            {
                if (Quantity < 0)
                    yield return new ValidationResult("Quantity must be positive for this action.", new[] { nameof(Quantity) });
            }

            if (ActionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(Notes))
                yield return new ValidationResult("Notes are required for an Adjustment.", new[] { nameof(Notes) });

            if (ActionType == StockActionType.CheckIn)
            {
                if (!ToLocationID.HasValue || ToLocationID.Value < 1)
                    yield return new ValidationResult("Please select a location for Check In.", new[] { nameof(ToLocationID) });
            }

            if (ActionType == StockActionType.CheckOut || ActionType == StockActionType.Adjustment)
            {
                if (!FromLocationID.HasValue || FromLocationID.Value < 1)
                    yield return new ValidationResult("Please select a location.", new[] { nameof(FromLocationID) });
            }

            if (ActionType == StockActionType.Transfer)
            {
                if (!FromLocationID.HasValue || FromLocationID.Value < 1)
                    yield return new ValidationResult("Please select a source location.", new[] { nameof(FromLocationID) });

                if (!ToLocationID.HasValue || ToLocationID.Value < 1)
                    yield return new ValidationResult("Please select a target location.", new[] { nameof(ToLocationID) });

                if (FromLocationID.HasValue && ToLocationID.HasValue && FromLocationID.Value == ToLocationID.Value)
                    yield return new ValidationResult("Target location must be different from source location.", new[] { nameof(ToLocationID) });
            }
        }
    }

    public class LocationStockVM
    {
        public int LocationID { get; set; }
        public string LocationName { get; set; } = string.Empty;
        public int QuantityOnHand { get; set; }
    }
}