using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using InvenTrack.Models;

namespace InvenTrack.ViewModels
{
    public class StockTransactionCreateVM : IValidatableObject
    {
        public int InventoryItemID { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;

        public int CurrentLocationID { get; set; }
        public string CurrentLocationName { get; set; } = string.Empty;

        public int CurrentQuantityOnHand { get; set; }

        [Display(Name = "Action")]
        public StockActionType ActionType { get; set; } = StockActionType.CheckIn;

        [Display(Name = "Quantity")]
        public int Quantity { get; set; }

        [StringLength(250)]
        public string? Notes { get; set; }

        [Display(Name = "Performed By")]
        [StringLength(120)]
        public string? PerformedBy { get; set; }

        [Display(Name = "Transfer To Location")]
        public int? TargetLocationID { get; set; }

        public IEnumerable<SelectListItem>? LocationOptions { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Quantity == 0)
            {
                yield return new ValidationResult("Quantity cannot be 0.", new[] { nameof(Quantity) });
            }

            if ((ActionType == StockActionType.CheckIn || ActionType == StockActionType.CheckOut || ActionType == StockActionType.Transfer) && Quantity < 0)
            {
                yield return new ValidationResult("Quantity must be positive for this action.", new[] { nameof(Quantity) });
            }

            if (ActionType == StockActionType.Adjustment && string.IsNullOrWhiteSpace(Notes))
            {
                yield return new ValidationResult("Notes are required for an Adjustment.", new[] { nameof(Notes) });
            }

            if (ActionType == StockActionType.Transfer)
            {
                if (!TargetLocationID.HasValue || TargetLocationID.Value < 1)
                    yield return new ValidationResult("Please select a target location.", new[] { nameof(TargetLocationID) });

                if (TargetLocationID.HasValue && TargetLocationID.Value == CurrentLocationID)
                    yield return new ValidationResult("Target location must be different from current location.", new[] { nameof(TargetLocationID) });
            }
        }
    }
}