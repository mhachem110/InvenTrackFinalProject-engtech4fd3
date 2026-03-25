namespace InvenTrack.ViewModels.Reports
{
    public class CategoryStockSummaryRowVM
    {
        public string CategoryName { get; set; } = "-";
        public int ItemCount { get; set; }
        public int ActiveItemCount { get; set; }
        public int TotalUnits { get; set; }
        public int LowStockCount { get; set; }
    }
}
