namespace InvenTrack.ViewModels.Reports
{
    public class LocationStockSummaryRowVM
    {
        public int StorageLocationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Building { get; set; }
        public string? Room { get; set; }
        public int ActiveItemCount { get; set; }
        public int TotalUnits { get; set; }
    }
}