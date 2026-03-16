namespace InvenTrack.ViewModels.AI
{
    public class AiInsightsDashboardVM
    {
        public string? Query { get; set; }

        public int LookbackDays { get; set; } = 60;
        public int TargetCoverageDays { get; set; } = 21;
        public int ReorderSoonDays { get; set; } = 14;

        public string AlertFilter { get; set; } = "all";
        public string CategoryFilter { get; set; } = "all";
        public string LocationFilter { get; set; } = "all";
        public string SortBy { get; set; } = "urgency";

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalPages { get; set; } = 1;
        public int TotalFilteredItems { get; set; }
        public int StartItemNumber { get; set; }
        public int EndItemNumber { get; set; }

        public int TotalItemsAnalyzed { get; set; }
        public int ReorderNowCount { get; set; }
        public int ReorderSoonCount { get; set; }
        public int WatchCount { get; set; }
        public int StableCount { get; set; }
        public int InsufficientDataCount { get; set; }

        public List<string> AvailableCategories { get; set; } = new();
        public List<string> AvailableLocations { get; set; } = new();

        public List<InventoryAiPredictionVM> TopPriorityItems { get; set; } = new();

        public InventoryAiPredictionVM? HighestUsageItem { get; set; }
        public InventoryAiPredictionVM? ClosestToReorderItem { get; set; }
        public InventoryAiPredictionVM? LargestSuggestedReorderItem { get; set; }

        public List<InventoryAiPredictionVM> Items { get; set; } = new();

        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(Query) ||
            !string.Equals(AlertFilter, "all", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(CategoryFilter, "all", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(LocationFilter, "all", StringComparison.OrdinalIgnoreCase);

        public string SortLabel => SortBy switch
        {
            "soonest" => "Soonest Reorder",
            "usage_desc" => "Highest Usage",
            "reorder_qty_desc" => "Largest Reorder Suggestion",
            "stock_asc" => "Lowest Current Stock",
            "item_asc" => "Item A-Z",
            "category_asc" => "Category A-Z",
            _ => "Urgency"
        };
    }

    public class InventoryAiPredictionVM
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "-";
        public string PrimaryLocationName { get; set; } = "-";
        public bool IsActive { get; set; }

        public int CurrentQuantityOnHand { get; set; }
        public int ReorderLevel { get; set; }
        public int LookbackDays { get; set; }
        public int DaysWithUsage { get; set; }
        public int DemandSignalCount { get; set; }
        public int TotalUnitsConsumed { get; set; }
        public double AverageDailyUsage { get; set; }
        public double? DaysUntilReorder { get; set; }
        public DateTime? PredictedReorderDateUtc { get; set; }
        public int SuggestedReorderQuantity { get; set; }

        public bool IsLowStockNow { get; set; }
        public bool IsPredictionAvailable { get; set; }
        public string AlertLevel { get; set; } = "InsufficientData";
        public string InsightSummary { get; set; } = "Not enough usage history yet.";
        public string RecommendedAction { get; set; } = "Capture more stock activity before relying on the AI suggestion.";

        public string AlertLabel => AlertLevel switch
        {
            "ReorderNow" => "Reorder Now",
            "ReorderSoon" => "Reorder Soon",
            "Watch" => "Watch",
            "Stable" => "Stable",
            _ => "Insufficient Data"
        };

        public string BadgeClass => AlertLevel switch
        {
            "ReorderNow" => "text-bg-danger",
            "ReorderSoon" => "text-bg-warning",
            "Watch" => "bg-info-subtle text-info border",
            "Stable" => "bg-success-subtle text-success border",
            _ => "bg-secondary-subtle text-secondary border"
        };

        public int AlertPriority => AlertLevel switch
        {
            "ReorderNow" => 0,
            "ReorderSoon" => 1,
            "Watch" => 2,
            "InsufficientData" => 3,
            _ => 4
        };

        public int UnitsAboveReorder => CurrentQuantityOnHand - ReorderLevel;

        public string UnitsAboveReorderDisplay =>
            UnitsAboveReorder > 0 ? $"+{UnitsAboveReorder}" : UnitsAboveReorder.ToString();

        public string AverageDailyUsageDisplay => AverageDailyUsage.ToString("0.##");

        public string DaysUntilReorderDisplay => DaysUntilReorder.HasValue
            ? Math.Max(0, Math.Ceiling(DaysUntilReorder.Value)).ToString("0")
            : "—";

        public string PredictedReorderDateDisplay => PredictedReorderDateUtc.HasValue
            ? PredictedReorderDateUtc.Value.ToLocalTime().ToString("yyyy-MM-dd")
            : "—";

        public int ConfidenceScore
        {
            get
            {
                if (!IsPredictionAvailable)
                    return 20;

                var score = 35;
                score += Math.Min(35, DemandSignalCount * 5);
                score += Math.Min(30, DaysWithUsage * 5);
                return Math.Min(100, score);
            }
        }

        public string ConfidenceLabel => ConfidenceScore switch
        {
            >= 80 => "High",
            >= 55 => "Medium",
            _ => "Low"
        };

        public string ConfidenceBadgeClass => ConfidenceLabel switch
        {
            "High" => "bg-success-subtle text-success border",
            "Medium" => "bg-warning-subtle text-warning border",
            _ => "bg-secondary-subtle text-secondary border"
        };

        public string ShortActionLabel => AlertLevel switch
        {
            "ReorderNow" => "Act Now",
            "ReorderSoon" => "Plan Order",
            "Watch" => "Monitor",
            "Stable" => "No Action",
            _ => "Need More Data"
        };
    }
}