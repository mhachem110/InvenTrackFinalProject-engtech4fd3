using System.ComponentModel.DataAnnotations;

namespace InvenTrack.ViewModels.Orders
{
    public class ReorderRequestCreateVM
    {
        [Required]
        public int InventoryItemID { get; set; }

        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int CurrentVisibleQuantity { get; set; }
        public int SuggestedQuantity { get; set; }
        public int ReorderLevel { get; set; }
        public bool RequiresApproval { get; set; }
        public string RequestModeLabel => RequiresApproval ? "Request Restock" : "Restock Now";

        public string AiConfidenceLabel { get; set; } = "Low";
        public string AiInsightSummary { get; set; } = string.Empty;
        public string SuggestedByLabel { get; set; } = "AI-assisted suggestion";
        public string DestinationHint { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Quantity to Order")]
        [Range(1, 100000)]
        public int RequestedQuantity { get; set; }

        [Required]
        [Display(Name = "Add stock to")]
        public int DestinationStorageLocationID { get; set; }

        [Display(Name = "Locations included in this order")]
        public List<int> RelatedLocationIds { get; set; } = new();

        [StringLength(500)]
        public string? Notes { get; set; }

        public List<LocationOptionVM> AvailableLocations { get; set; } = new();
    }
}
