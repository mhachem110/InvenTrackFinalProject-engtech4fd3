using InvenTrack.Models;

namespace InvenTrack.ViewModels.Orders
{
    public class OrderRequestRowVM
    {
        public int ID { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string DestinationLocation { get; set; } = string.Empty;
        public string RelatedLocations { get; set; } = string.Empty;
        public int CurrentVisibleQuantity { get; set; }
        public int SuggestedQuantity { get; set; }
        public int RequestedQuantity { get; set; }
        public string RequestedByName { get; set; } = string.Empty;
        public DateTime DateRequested { get; set; }
        public OrderRequestStatus Status { get; set; }
        public string? ReviewedByName { get; set; }
        public DateTime? DateReviewed { get; set; }
        public string? ReviewDecision { get; set; }
        public string? FulfilledBy { get; set; }
        public DateTime? DateFulfilled { get; set; }
        public bool CanApprove { get; set; }
        public bool CanReject { get; set; }
        public string? Notes { get; set; }
    }
}
