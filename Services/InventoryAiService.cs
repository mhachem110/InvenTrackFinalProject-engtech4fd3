using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.ViewModels.AI;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Services
{
    public class InventoryAiService
    {
        private readonly InvenTrackContext _context;

        public InventoryAiService(InvenTrackContext context)
        {
            _context = context;
        }

        public async Task<InventoryAiPredictionVM?> GetPredictionAsync(
            int inventoryItemId,
            AccessScope scope,
            int lookbackDays = 60,
            int targetCoverageDays = 21,
            int reorderSoonDays = 14)
        {
            lookbackDays = Clamp(lookbackDays, 14, 180);
            targetCoverageDays = Clamp(targetCoverageDays, 7, 60);
            reorderSoonDays = Clamp(reorderSoonDays, 3, 45);

            var itemQuery = ApplyInventoryScope(
                _context.InventoryItems
                    .AsNoTracking()
                    .Include(i => i.InventoryItemStocks),
                scope);

            var item = await itemQuery
                .Select(i => new ItemSnapshot
                {
                    ItemId = i.ID,
                    ItemName = i.ItemName,
                    SKU = i.SKU,
                    CategoryName = i.Category != null ? i.Category.Name : "-",
                    PrimaryLocationName = i.StorageLocation != null ? i.StorageLocation.Name : "-",
                    IsActive = i.IsActive,
                    ReorderLevel = i.ReorderLevel,
                    FallbackQuantityOnHand = i.QuantityOnHand,
                    PrimaryLocationId = i.StorageLocationID
                })
                .FirstOrDefaultAsync(i => i.ItemId == inventoryItemId);

            if (item == null)
                return null;

            var stocks = await LoadStocksAsync(new List<int> { inventoryItemId }, scope);
            var transactions = await LoadTransactionsAsync(new List<int> { inventoryItemId }, scope, lookbackDays);

            return BuildPrediction(item, stocks, transactions, scope, lookbackDays, targetCoverageDays, reorderSoonDays);
        }

        public async Task<AiInsightsDashboardVM> BuildDashboardAsync(
            AccessScope scope,
            string? q = null,
            int lookbackDays = 60,
            int targetCoverageDays = 21,
            int reorderSoonDays = 14)
        {
            lookbackDays = Clamp(lookbackDays, 14, 180);
            targetCoverageDays = Clamp(targetCoverageDays, 7, 60);
            reorderSoonDays = Clamp(reorderSoonDays, 3, 45);

            var itemQuery = ApplyInventoryScope(_context.InventoryItems.AsNoTracking(), scope)
                .Where(i => i.IsActive);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                itemQuery = itemQuery.Where(i =>
                    i.ItemName.Contains(q) ||
                    i.SKU.Contains(q) ||
                    (i.Description != null && i.Description.Contains(q)) ||
                    (i.Category != null && i.Category.Name.Contains(q)) ||
                    (i.StorageLocation != null && i.StorageLocation.Name.Contains(q)));
            }

            var items = await itemQuery
                .Select(i => new ItemSnapshot
                {
                    ItemId = i.ID,
                    ItemName = i.ItemName,
                    SKU = i.SKU,
                    CategoryName = i.Category != null ? i.Category.Name : "-",
                    PrimaryLocationName = i.StorageLocation != null ? i.StorageLocation.Name : "-",
                    IsActive = i.IsActive,
                    ReorderLevel = i.ReorderLevel,
                    FallbackQuantityOnHand = i.QuantityOnHand,
                    PrimaryLocationId = i.StorageLocationID
                })
                .ToListAsync();

            var itemIds = items.Select(i => i.ItemId).ToList();
            var stocks = await LoadStocksAsync(itemIds, scope);
            var transactions = await LoadTransactionsAsync(itemIds, scope, lookbackDays);

            var results = items
                .Select(item => BuildPrediction(item, stocks, transactions, scope, lookbackDays, targetCoverageDays, reorderSoonDays))
                .OrderBy(x => x.AlertPriority)
                .ThenBy(x => x.DaysUntilReorder ?? double.MaxValue)
                .ThenBy(x => x.ItemName)
                .ToList();

            return new AiInsightsDashboardVM
            {
                Query = q,
                LookbackDays = lookbackDays,
                TargetCoverageDays = targetCoverageDays,
                ReorderSoonDays = reorderSoonDays,
                TotalItemsAnalyzed = results.Count,
                ReorderNowCount = results.Count(x => x.AlertLevel == "ReorderNow"),
                ReorderSoonCount = results.Count(x => x.AlertLevel == "ReorderSoon"),
                WatchCount = results.Count(x => x.AlertLevel == "Watch"),
                StableCount = results.Count(x => x.AlertLevel == "Stable"),
                InsufficientDataCount = results.Count(x => x.AlertLevel == "InsufficientData"),
                Items = results
            };
        }

        public double? EstimateDaysUntilReorder(int currentQuantity, int reorderLevel, double averageDailyUsage)
        {
            if (averageDailyUsage <= 0)
                return null;

            if (currentQuantity <= reorderLevel)
                return 0;

            return Math.Max(0, (currentQuantity - reorderLevel) / averageDailyUsage);
        }

        private async Task<List<StockSnapshot>> LoadStocksAsync(List<int> itemIds, AccessScope scope)
        {
            if (itemIds.Count == 0)
                return new List<StockSnapshot>();

            var query = _context.InventoryItemStocks
                .AsNoTracking()
                .Where(s => itemIds.Contains(s.InventoryItemID));

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                query = query.Where(s => s.StorageLocationID == locationId);
            }

            return await query
                .Select(s => new StockSnapshot
                {
                    InventoryItemId = s.InventoryItemID,
                    StorageLocationId = s.StorageLocationID,
                    QuantityOnHand = s.QuantityOnHand
                })
                .ToListAsync();
        }

        private async Task<List<TransactionSnapshot>> LoadTransactionsAsync(List<int> itemIds, AccessScope scope, int lookbackDays)
        {
            if (itemIds.Count == 0)
                return new List<TransactionSnapshot>();

            var sinceUtc = DateTime.UtcNow.Date.AddDays(-lookbackDays);

            var query = _context.StockTransactions
                .AsNoTracking()
                .Where(t => itemIds.Contains(t.InventoryItemID) && t.DateCreated >= sinceUtc);

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                query = query.Where(t =>
                    t.FromStorageLocationID == locationId ||
                    t.ToStorageLocationID == locationId);
            }

            return await query
                .Select(t => new TransactionSnapshot
                {
                    InventoryItemId = t.InventoryItemID,
                    ActionType = t.ActionType,
                    QuantityChange = t.QuantityChange,
                    DateCreated = t.DateCreated,
                    FromStorageLocationId = t.FromStorageLocationID,
                    ToStorageLocationId = t.ToStorageLocationID
                })
                .ToListAsync();
        }

        private InventoryAiPredictionVM BuildPrediction(
            ItemSnapshot item,
            List<StockSnapshot> allStocks,
            List<TransactionSnapshot> allTransactions,
            AccessScope scope,
            int lookbackDays,
            int targetCoverageDays,
            int reorderSoonDays)
        {
            var itemStocks = allStocks.Where(s => s.InventoryItemId == item.ItemId).ToList();
            var currentQuantity = GetVisibleQuantity(item, itemStocks, scope);

            var signals = allTransactions
                .Where(t => t.InventoryItemId == item.ItemId)
                .Select(t => new
                {
                    t.DateCreated,
                    Usage = GetDemandSignalQuantity(t, scope)
                })
                .Where(x => x.Usage > 0)
                .OrderBy(x => x.DateCreated)
                .ToList();

            var usageTxnCount = signals.Count;
            var daysWithUsage = signals.Select(x => x.DateCreated.Date).Distinct().Count();
            var totalConsumed = signals.Sum(x => x.Usage);
            var avgDailyUsage = totalConsumed > 0 ? totalConsumed / (double)lookbackDays : 0;

            var result = new InventoryAiPredictionVM
            {
                ItemId = item.ItemId,
                ItemName = item.ItemName,
                SKU = item.SKU,
                CategoryName = item.CategoryName,
                PrimaryLocationName = GetVisiblePrimaryLocationName(item, scope),
                IsActive = item.IsActive,
                CurrentQuantityOnHand = currentQuantity,
                ReorderLevel = item.ReorderLevel,
                LookbackDays = lookbackDays,
                DaysWithUsage = daysWithUsage,
                DemandSignalCount = usageTxnCount,
                TotalUnitsConsumed = totalConsumed,
                AverageDailyUsage = avgDailyUsage,
                IsLowStockNow = currentQuantity <= item.ReorderLevel
            };

            var hasEnoughData = usageTxnCount >= 3 && daysWithUsage >= 2 && totalConsumed > 0 && avgDailyUsage > 0;
            result.IsPredictionAvailable = hasEnoughData;

            if (result.IsLowStockNow)
            {
                result.AlertLevel = "ReorderNow";
                result.DaysUntilReorder = 0;
                result.PredictedReorderDateUtc = DateTime.UtcNow.Date;
                result.SuggestedReorderQuantity = Math.Max(
                    0,
                    (int)Math.Ceiling(item.ReorderLevel + (targetCoverageDays * Math.Max(avgDailyUsage, 1)) - currentQuantity));
                result.InsightSummary = "Current stock is already at or below the reorder level.";
                result.RecommendedAction = "Place a replenishment order immediately and review recent usage.";
                return result;
            }

            if (!hasEnoughData)
            {
                result.AlertLevel = "InsufficientData";
                result.DaysUntilReorder = null;
                result.PredictedReorderDateUtc = null;
                result.SuggestedReorderQuantity = 0;
                result.InsightSummary = "There is not enough recent outbound activity to produce a dependable prediction yet.";
                result.RecommendedAction = "Record more check-outs or adjustments before using the AI forecast for replenishment.";
                return result;
            }

            var daysUntilReorder = EstimateDaysUntilReorder(currentQuantity, item.ReorderLevel, avgDailyUsage);
            result.DaysUntilReorder = daysUntilReorder;
            result.PredictedReorderDateUtc = daysUntilReorder.HasValue
                ? DateTime.UtcNow.Date.AddDays(daysUntilReorder.Value)
                : null;

            result.SuggestedReorderQuantity = Math.Max(
                0,
                (int)Math.Ceiling(item.ReorderLevel + (targetCoverageDays * avgDailyUsage) - currentQuantity));

            if (!daysUntilReorder.HasValue)
            {
                result.AlertLevel = "InsufficientData";
                result.InsightSummary = "Usage exists, but the average daily demand is too small to forecast confidently.";
                result.RecommendedAction = "Keep monitoring transactions and review the item manually.";
            }
            else if (daysUntilReorder.Value <= 7)
            {
                result.AlertLevel = "ReorderNow";
                result.InsightSummary = $"At the recent usage rate, this item is expected to reach the reorder threshold in about {Math.Max(0, Math.Ceiling(daysUntilReorder.Value)):0} day(s).";
                result.RecommendedAction = "Trigger a replenishment request now and notify the responsible manager.";
            }
            else if (daysUntilReorder.Value <= reorderSoonDays)
            {
                result.AlertLevel = "ReorderSoon";
                result.InsightSummary = $"At the recent usage rate, this item is expected to reach the reorder threshold in about {Math.Ceiling(daysUntilReorder.Value):0} day(s).";
                result.RecommendedAction = "Prepare a replenishment order soon and monitor check-outs daily.";
            }
            else if (daysUntilReorder.Value <= reorderSoonDays * 2)
            {
                result.AlertLevel = "Watch";
                result.InsightSummary = $"Usage trend suggests the item should stay above its reorder level for roughly {Math.Ceiling(daysUntilReorder.Value):0} day(s), but it is moving toward the threshold.";
                result.RecommendedAction = "Keep this item on the watchlist and verify the next outgoing transactions.";
            }
            else
            {
                result.AlertLevel = "Stable";
                result.InsightSummary = $"Current usage trend shows comfortable stock coverage for roughly {Math.Ceiling(daysUntilReorder.Value):0} day(s).";
                result.RecommendedAction = "No immediate action is needed beyond normal monitoring.";
            }

            return result;
        }

        private static int GetVisibleQuantity(ItemSnapshot item, List<StockSnapshot> stocks, AccessScope scope)
        {
            if (stocks.Count > 0)
                return stocks.Sum(s => s.QuantityOnHand);

            if (scope.HasGlobalLocationAccess)
                return item.FallbackQuantityOnHand;

            return item.PrimaryLocationId == scope.AssignedLocationId
                ? item.FallbackQuantityOnHand
                : 0;
        }

        private static string GetVisiblePrimaryLocationName(ItemSnapshot item, AccessScope scope)
        {
            if (scope.IsScopedUser && !string.IsNullOrWhiteSpace(scope.AssignedLocationName))
                return scope.AssignedLocationName!;

            return item.PrimaryLocationName;
        }

        private static int GetDemandSignalQuantity(TransactionSnapshot transaction, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
            {
                if (transaction.ActionType == StockActionType.CheckOut && transaction.QuantityChange < 0)
                    return Math.Abs(transaction.QuantityChange);

                if (transaction.ActionType == StockActionType.Adjustment && transaction.QuantityChange < 0)
                    return Math.Abs(transaction.QuantityChange);

                return 0;
            }

            var locationId = scope.AssignedLocationId!.Value;

            if (transaction.FromStorageLocationId != locationId)
                return 0;

            if (transaction.ActionType == StockActionType.CheckOut && transaction.QuantityChange < 0)
                return Math.Abs(transaction.QuantityChange);

            if (transaction.ActionType == StockActionType.Adjustment && transaction.QuantityChange < 0)
                return Math.Abs(transaction.QuantityChange);

            return 0;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Min(max, Math.Max(min, value));
        }

        private static IQueryable<InventoryItem> ApplyInventoryScope(IQueryable<InventoryItem> query, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return query;

            var locationId = scope.AssignedLocationId!.Value;

            return query.Where(i =>
                i.StorageLocationID == locationId ||
                i.InventoryItemStocks.Any(s => s.StorageLocationID == locationId));
        }

        private sealed class ItemSnapshot
        {
            public int ItemId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string SKU { get; set; } = string.Empty;
            public string CategoryName { get; set; } = "-";
            public string PrimaryLocationName { get; set; } = "-";
            public bool IsActive { get; set; }
            public int ReorderLevel { get; set; }
            public int FallbackQuantityOnHand { get; set; }
            public int PrimaryLocationId { get; set; }
        }

        private sealed class StockSnapshot
        {
            public int InventoryItemId { get; set; }
            public int StorageLocationId { get; set; }
            public int QuantityOnHand { get; set; }
        }

        private sealed class TransactionSnapshot
        {
            public int InventoryItemId { get; set; }
            public StockActionType ActionType { get; set; }
            public int QuantityChange { get; set; }
            public DateTime DateCreated { get; set; }
            public int? FromStorageLocationId { get; set; }
            public int? ToStorageLocationId { get; set; }
        }
    }
}