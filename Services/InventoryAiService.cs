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
            var locationNames = await LoadLocationNamesAsync(stocks.Select(x => x.StorageLocationId).Distinct().ToList());

            return BuildPrediction(item, stocks, transactions, locationNames, scope, lookbackDays, targetCoverageDays, reorderSoonDays);
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
            var locationNames = await LoadLocationNamesAsync(stocks.Select(x => x.StorageLocationId).Distinct().ToList());

            var results = items
                .Select(item => BuildPrediction(item, stocks, transactions, locationNames, scope, lookbackDays, targetCoverageDays, reorderSoonDays))
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

        private async Task<Dictionary<int, string>> LoadLocationNamesAsync(List<int> locationIds)
        {
            if (locationIds.Count == 0)
                return new Dictionary<int, string>();

            return await _context.StorageLocations
                .AsNoTracking()
                .Where(x => locationIds.Contains(x.ID))
                .ToDictionaryAsync(x => x.ID, x => x.Name);
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
            Dictionary<int, string> locationNames,
            AccessScope scope,
            int lookbackDays,
            int targetCoverageDays,
            int reorderSoonDays)
        {
            var itemStocks = allStocks.Where(s => s.InventoryItemId == item.ItemId).ToList();
            var locationPredictions = BuildLocationPredictions(
                item,
                itemStocks,
                allTransactions.Where(t => t.InventoryItemId == item.ItemId).ToList(),
                locationNames,
                scope,
                lookbackDays,
                targetCoverageDays,
                reorderSoonDays);

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

            var result = FinalizePrediction(new InventoryAiPredictionVM
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
                IsLowStockNow = currentQuantity <= item.ReorderLevel,
                LocationPredictions = locationPredictions,
                IsAggregatePrediction = scope.HasGlobalLocationAccess && locationPredictions.Count > 1
            }, targetCoverageDays, reorderSoonDays);

            if (scope.HasGlobalLocationAccess && locationPredictions.Count > 0)
            {
                var highestRisk = locationPredictions
                    .OrderBy(x => x.AlertPriority)
                    .ThenBy(x => x.DaysUntilReorder ?? double.MaxValue)
                    .First();

                result.RecommendedAction = locationPredictions.Count > 1
                    ? $"{result.RecommendedAction} Highest location risk: {highestRisk.LocationName}."
                    : result.RecommendedAction;

                result.InsightSummary = locationPredictions.Count > 1
                    ? $"{result.InsightSummary} Review the per-location breakdown below for transfer or replenishment decisions."
                    : result.InsightSummary;
            }

            return result;
        }

        private List<LocationAiPredictionVM> BuildLocationPredictions(
            ItemSnapshot item,
            List<StockSnapshot> itemStocks,
            List<TransactionSnapshot> itemTransactions,
            Dictionary<int, string> locationNames,
            AccessScope scope,
            int lookbackDays,
            int targetCoverageDays,
            int reorderSoonDays)
        {
            var locations = new List<LocationAiPredictionVM>();

            if (scope.IsScopedUser && scope.AssignedLocationId.HasValue)
            {
                var stock = itemStocks.FirstOrDefault(x => x.StorageLocationId == scope.AssignedLocationId.Value);
                locations.Add(FinalizeLocationPrediction(new LocationAiPredictionVM
                {
                    StorageLocationId = scope.AssignedLocationId.Value,
                    LocationName = scope.AssignedLocationName ?? "-",
                    CurrentQuantityOnHand = stock?.QuantityOnHand ?? (item.PrimaryLocationId == scope.AssignedLocationId ? item.FallbackQuantityOnHand : 0),
                    ReorderLevel = item.ReorderLevel,
                    DemandSignalCount = 0,
                    DaysWithUsage = 0,
                    TotalUnitsConsumed = 0,
                    AverageDailyUsage = 0,
                    IsLowStockNow = (stock?.QuantityOnHand ?? 0) <= item.ReorderLevel
                }, itemTransactions, scope.AssignedLocationId.Value, lookbackDays, targetCoverageDays, reorderSoonDays));
                return locations;
            }

            var stockLocationIds = itemStocks.Select(x => x.StorageLocationId).Distinct().ToList();
            if (stockLocationIds.Count == 0)
            {
                stockLocationIds.Add(item.PrimaryLocationId);
            }

            foreach (var locationId in stockLocationIds.Distinct())
            {
                var stock = itemStocks.FirstOrDefault(x => x.StorageLocationId == locationId);
                locations.Add(FinalizeLocationPrediction(new LocationAiPredictionVM
                {
                    StorageLocationId = locationId,
                    LocationName = locationNames.TryGetValue(locationId, out var name) ? name : (locationId == item.PrimaryLocationId ? item.PrimaryLocationName : "-"),
                    CurrentQuantityOnHand = stock?.QuantityOnHand ?? (locationId == item.PrimaryLocationId ? item.FallbackQuantityOnHand : 0),
                    ReorderLevel = item.ReorderLevel,
                    IsLowStockNow = (stock?.QuantityOnHand ?? 0) <= item.ReorderLevel
                }, itemTransactions, locationId, lookbackDays, targetCoverageDays, reorderSoonDays));
            }

            return locations
                .OrderBy(x => x.AlertPriority)
                .ThenBy(x => x.DaysUntilReorder ?? double.MaxValue)
                .ThenBy(x => x.LocationName)
                .ToList();
        }

        private InventoryAiPredictionVM FinalizePrediction(
            InventoryAiPredictionVM result,
            int targetCoverageDays,
            int reorderSoonDays)
        {
            var hasEnoughData = result.DemandSignalCount >= 3 &&
                                result.DaysWithUsage >= 2 &&
                                result.TotalUnitsConsumed > 0 &&
                                result.AverageDailyUsage > 0;

            result.IsPredictionAvailable = hasEnoughData;

            if (result.IsLowStockNow)
            {
                result.AlertLevel = "ReorderNow";
                result.DaysUntilReorder = 0;
                result.PredictedReorderDateUtc = DateTime.UtcNow.Date;
                result.SuggestedReorderQuantity = Math.Max(
                    0,
                    (int)Math.Ceiling(result.ReorderLevel + (targetCoverageDays * Math.Max(result.AverageDailyUsage, 1)) - result.CurrentQuantityOnHand));
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

            var daysUntilReorder = EstimateDaysUntilReorder(result.CurrentQuantityOnHand, result.ReorderLevel, result.AverageDailyUsage);
            result.DaysUntilReorder = daysUntilReorder;
            result.PredictedReorderDateUtc = daysUntilReorder.HasValue
                ? DateTime.UtcNow.Date.AddDays(daysUntilReorder.Value)
                : null;

            result.SuggestedReorderQuantity = Math.Max(
                0,
                (int)Math.Ceiling(result.ReorderLevel + (targetCoverageDays * result.AverageDailyUsage) - result.CurrentQuantityOnHand));

            ApplyAlertState(
                result.CurrentQuantityOnHand,
                result.ReorderLevel,
                result.AverageDailyUsage,
                result.DaysUntilReorder,
                reorderSoonDays,
                out var alertLevel,
                out var insight,
                out var action);

            result.AlertLevel = alertLevel;
            result.InsightSummary = insight;
            result.RecommendedAction = action;
            return result;
        }

        private LocationAiPredictionVM FinalizeLocationPrediction(
            LocationAiPredictionVM result,
            List<TransactionSnapshot> itemTransactions,
            int locationId,
            int lookbackDays,
            int targetCoverageDays,
            int reorderSoonDays)
        {
            var signals = itemTransactions
                .Select(t => new
                {
                    t.DateCreated,
                    Usage = GetDemandSignalQuantityForLocation(t, locationId)
                })
                .Where(x => x.Usage > 0)
                .OrderBy(x => x.DateCreated)
                .ToList();

            result.DemandSignalCount = signals.Count;
            result.DaysWithUsage = signals.Select(x => x.DateCreated.Date).Distinct().Count();
            result.TotalUnitsConsumed = signals.Sum(x => x.Usage);
            result.AverageDailyUsage = result.TotalUnitsConsumed > 0
                ? result.TotalUnitsConsumed / (double)lookbackDays
                : 0;
            result.IsLowStockNow = result.CurrentQuantityOnHand <= result.ReorderLevel;

            var hasEnoughData = result.DemandSignalCount >= 3 &&
                                result.DaysWithUsage >= 2 &&
                                result.TotalUnitsConsumed > 0 &&
                                result.AverageDailyUsage > 0;

            result.IsPredictionAvailable = hasEnoughData;

            if (result.IsLowStockNow)
            {
                result.AlertLevel = "ReorderNow";
                result.DaysUntilReorder = 0;
                result.PredictedReorderDateUtc = DateTime.UtcNow.Date;
                result.SuggestedReorderQuantity = Math.Max(
                    0,
                    (int)Math.Ceiling(result.ReorderLevel + (targetCoverageDays * Math.Max(result.AverageDailyUsage, 1)) - result.CurrentQuantityOnHand));
                result.InsightSummary = "This location is already at or below its reorder threshold.";
                return result;
            }

            if (!hasEnoughData)
            {
                result.AlertLevel = "InsufficientData";
                result.DaysUntilReorder = null;
                result.PredictedReorderDateUtc = null;
                result.SuggestedReorderQuantity = 0;
                result.InsightSummary = "This location does not have enough recent demand signals yet.";
                return result;
            }

            result.DaysUntilReorder = EstimateDaysUntilReorder(result.CurrentQuantityOnHand, result.ReorderLevel, result.AverageDailyUsage);
            result.PredictedReorderDateUtc = result.DaysUntilReorder.HasValue
                ? DateTime.UtcNow.Date.AddDays(result.DaysUntilReorder.Value)
                : null;
            result.SuggestedReorderQuantity = Math.Max(
                0,
                (int)Math.Ceiling(result.ReorderLevel + (targetCoverageDays * result.AverageDailyUsage) - result.CurrentQuantityOnHand));

            ApplyAlertState(
                result.CurrentQuantityOnHand,
                result.ReorderLevel,
                result.AverageDailyUsage,
                result.DaysUntilReorder,
                reorderSoonDays,
                out var alertLevel,
                out var insight,
                out _);

            result.AlertLevel = alertLevel;
            result.InsightSummary = insight;
            return result;
        }

        private void ApplyAlertState(
            int currentQuantity,
            int reorderLevel,
            double averageDailyUsage,
            double? daysUntilReorder,
            int reorderSoonDays,
            out string alertLevel,
            out string insightSummary,
            out string recommendedAction)
        {
            if (!daysUntilReorder.HasValue)
            {
                alertLevel = "InsufficientData";
                insightSummary = "Usage exists, but the average daily demand is too small to forecast confidently.";
                recommendedAction = "Keep monitoring transactions and review the item manually.";
            }
            else if (daysUntilReorder.Value <= 7)
            {
                alertLevel = "ReorderNow";
                insightSummary = $"At the recent usage rate, this item is expected to reach the reorder threshold in about {Math.Max(0, Math.Ceiling(daysUntilReorder.Value)):0} day(s).";
                recommendedAction = "Trigger a replenishment request now and notify the responsible manager.";
            }
            else if (daysUntilReorder.Value <= reorderSoonDays)
            {
                alertLevel = "ReorderSoon";
                insightSummary = $"At the recent usage rate, this item is expected to reach the reorder threshold in about {Math.Ceiling(daysUntilReorder.Value):0} day(s).";
                recommendedAction = "Prepare a replenishment order soon and monitor check-outs daily.";
            }
            else if (daysUntilReorder.Value <= reorderSoonDays * 2)
            {
                alertLevel = "Watch";
                insightSummary = $"Usage trend suggests the item should stay above its reorder level for roughly {Math.Ceiling(daysUntilReorder.Value):0} day(s), but it is moving toward the threshold.";
                recommendedAction = "Keep this item on the watchlist and verify the next outgoing transactions.";
            }
            else
            {
                alertLevel = "Stable";
                insightSummary = $"Current usage trend shows comfortable stock coverage for roughly {Math.Ceiling(daysUntilReorder.Value):0} day(s).";
                recommendedAction = "No immediate action is needed beyond normal monitoring.";
            }
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
            return GetDemandSignalQuantityForLocation(transaction, locationId);
        }

        private static int GetDemandSignalQuantityForLocation(TransactionSnapshot transaction, int locationId)
        {
            if (transaction.FromStorageLocationId != locationId)
                return 0;

            if (transaction.ActionType == StockActionType.CheckOut && transaction.QuantityChange < 0)
                return Math.Abs(transaction.QuantityChange);

            if (transaction.ActionType == StockActionType.Adjustment && transaction.QuantityChange < 0)
                return Math.Abs(transaction.QuantityChange);

            if (transaction.ActionType == StockActionType.Transfer && transaction.QuantityChange > 0)
                return transaction.QuantityChange;

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
