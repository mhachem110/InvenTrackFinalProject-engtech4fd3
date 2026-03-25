namespace InvenTrack.ViewModels.Reports
{
    public class InventoryMovementSummaryRowVM
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = "-";
        public string SKU { get; set; } = "-";
        public string CategoryName { get; set; } = "-";
        public int CheckInUnits { get; set; }
        public int CheckOutUnits { get; set; }
        public int AdjustmentNetUnits { get; set; }
        public int TransferOutUnits { get; set; }
        public int TransferInUnits { get; set; }
        public int TotalActivityCount { get; set; }
        public DateTime? LastActivityDateUtc { get; set; }
        public string LastActivityDateDisplay => LastActivityDateUtc.HasValue
            ? LastActivityDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
            : "—";
    }
}
