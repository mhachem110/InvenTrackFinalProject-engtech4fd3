namespace InvenTrack.ViewModels.Reports
{
    public class LowStockReportRowVM
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "-";
        public string PrimaryLocationName { get; set; } = "-";
        public int TotalQuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
        public int LocationCount { get; set; }
        public bool IsActive { get; set; }
    }
}