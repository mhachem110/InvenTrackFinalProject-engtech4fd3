using InvenTrack.Models;

namespace InvenTrack.ViewModels.Orders
{
    public class OrderRequestIndexVM
    {
        public List<OrderRequestRowVM> Rows { get; set; } = new();
        public string Search { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = string.Empty;
        public bool CanApprove { get; set; }
        public int PendingCount { get; set; }
    }
}
