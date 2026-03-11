namespace InvenTrack.ViewModels.Reports
{
    public class ReportIndexVM
    {
        public int InventoryItemCount { get; set; }
        public int LowStockItemCount { get; set; }
        public int StorageLocationCount { get; set; }
        public int RecentTransactionCount { get; set; }
    }
}