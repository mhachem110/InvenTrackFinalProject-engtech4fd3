using System;

namespace InvenTrack.ViewModels.Reports
{
    public class RecentTransactionReportRowVM
    {
        public int TransactionId { get; set; }
        public DateTime DateCreated { get; set; }
        public string ItemName { get; set; } = "-";
        public string SKU { get; set; } = "-";
        public string ActionType { get; set; } = string.Empty;
        public int QuantityChange { get; set; }
        public string FromLocationName { get; set; } = "-";
        public string ToLocationName { get; set; } = "-";
        public string PerformedBy { get; set; } = "-";
        public string Notes { get; set; } = "-";
    }
}