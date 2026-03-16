using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.Utilities;
using InvenTrack.ViewModels.AI;
using InvenTrack.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace InvenTrack.Controllers
{
    [Authorize(Roles =
        AppRoles.Admin + "," +
        AppRoles.RegionalManager + "," +
        AppRoles.Manager + "," +
        AppRoles.Supervisor)]
    public class ReportsController : Controller
    {
        private readonly InvenTrackContext _context;
        private readonly AppAccessService _accessService;
        private readonly InventoryAiService _inventoryAiService;

        public ReportsController(
            InvenTrackContext context,
            AppAccessService accessService,
            InventoryAiService inventoryAiService)
        {
            _context = context;
            _accessService = accessService;
            _inventoryAiService = inventoryAiService;
        }

        public async Task<IActionResult> Index()
        {
            var scope = await _accessService.GetScopeAsync(User);
            var aiDashboard = await _inventoryAiService.BuildDashboardAsync(scope);

            var vm = new ReportIndexVM
            {
                InventoryItemCount = await BuildInventoryReportQuery(scope, null).CountAsync(),
                StorageLocationCount = await BuildLocationSummaryQuery(scope, null).CountAsync(),
                RecentTransactionCount = await BuildRecentTransactionsQuery(scope, null, "all").CountAsync(),
                LowStockItemCount = await BuildLowStockQuery(scope, null).CountAsync()
            };

            ViewData["AiPredictedIssuesCount"] = aiDashboard.ReorderSoonCount + aiDashboard.ReorderNowCount;
            ViewData["AiUrgentIssuesCount"] = aiDashboard.ReorderNowCount;

            return View(vm);
        }

        public async Task<IActionResult> LowStock(string? q, int page = 1, int pageSize = 10)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            var query = BuildLowStockQuery(scope, q);

            var model = await PaginatedList<LowStockReportRowVM>.CreateAsync(
                query.OrderBy(i => i.TotalQuantityOnHand).ThenBy(i => i.ItemName),
                page,
                pageSize);

            if (model.TotalPages > 0 && page > model.TotalPages)
            {
                page = model.TotalPages;

                model = await PaginatedList<LowStockReportRowVM>.CreateAsync(
                    query.OrderBy(i => i.TotalQuantityOnHand).ThenBy(i => i.ItemName),
                    page,
                    pageSize);
            }

            ViewData["CurrentFilter"] = q ?? string.Empty;
            ViewData["CurrentPageSize"] = pageSize;

            return View(model);
        }

        public async Task<IActionResult> LocationSummary(string? q, int page = 1, int pageSize = 10)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            var query = BuildLocationSummaryQuery(scope, q);

            var model = await PaginatedList<LocationStockSummaryRowVM>.CreateAsync(
                query.OrderBy(x => x.Name),
                page,
                pageSize);

            if (model.TotalPages > 0 && page > model.TotalPages)
            {
                page = model.TotalPages;

                model = await PaginatedList<LocationStockSummaryRowVM>.CreateAsync(
                    query.OrderBy(x => x.Name),
                    page,
                    pageSize);
            }

            ViewData["CurrentFilter"] = q ?? string.Empty;
            ViewData["CurrentPageSize"] = pageSize;

            return View(model);
        }

        public async Task<IActionResult> RecentTransactions(
            string? q,
            string actionFilter = "all",
            int page = 1,
            int pageSize = 10)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            actionFilter = string.IsNullOrWhiteSpace(actionFilter)
                ? "all"
                : actionFilter.Trim().ToLowerInvariant();

            var query = BuildRecentTransactionsQuery(scope, q, actionFilter);

            var model = await PaginatedList<RecentTransactionReportRowVM>.CreateAsync(
                query.OrderByDescending(x => x.DateCreated),
                page,
                pageSize);

            if (model.TotalPages > 0 && page > model.TotalPages)
            {
                page = model.TotalPages;

                model = await PaginatedList<RecentTransactionReportRowVM>.CreateAsync(
                    query.OrderByDescending(x => x.DateCreated),
                    page,
                    pageSize);
            }

            ViewData["CurrentFilter"] = q ?? string.Empty;
            ViewData["CurrentAction"] = actionFilter;
            ViewData["CurrentPageSize"] = pageSize;

            return View(model);
        }

        public async Task<IActionResult> AiInsights(
            string? q,
            int lookbackDays = 60,
            int targetCoverageDays = 21,
            int reorderSoonDays = 14,
            string alertFilter = "all",
            string categoryFilter = "all",
            string locationFilter = "all",
            string sortBy = "urgency",
            int page = 1,
            int pageSize = 25)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50, 100 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 25;

            var model = await _inventoryAiService.BuildDashboardAsync(
                scope,
                q,
                lookbackDays,
                targetCoverageDays,
                reorderSoonDays);

            var allItems = model.Items ?? new List<InventoryAiPredictionVM>();

            model.AvailableCategories = allItems
                .Select(x => x.CategoryName)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "-")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            model.AvailableLocations = allItems
                .Select(x => x.PrimaryLocationName)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != "-")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            var filtered = FilterAiItems(allItems, alertFilter, categoryFilter, locationFilter);
            var urgencySorted = SortAiItems(filtered, "urgency");
            var sorted = SortAiItems(filtered, sortBy);

            model.Query = q;
            model.AlertFilter = string.IsNullOrWhiteSpace(alertFilter) ? "all" : alertFilter;
            model.CategoryFilter = string.IsNullOrWhiteSpace(categoryFilter) ? "all" : categoryFilter;
            model.LocationFilter = string.IsNullOrWhiteSpace(locationFilter) ? "all" : locationFilter;
            model.SortBy = string.IsNullOrWhiteSpace(sortBy) ? "urgency" : sortBy;
            model.PageSize = pageSize;

            model.TotalFilteredItems = sorted.Count;

            model.ReorderNowCount = filtered.Count(x => x.AlertLevel == "ReorderNow");
            model.ReorderSoonCount = filtered.Count(x => x.AlertLevel == "ReorderSoon");
            model.WatchCount = filtered.Count(x => x.AlertLevel == "Watch");
            model.StableCount = filtered.Count(x => x.AlertLevel == "Stable");
            model.InsufficientDataCount = filtered.Count(x => x.AlertLevel == "InsufficientData");

            model.TopPriorityItems = urgencySorted.Take(5).ToList();

            model.HighestUsageItem = filtered
                .Where(x => x.AverageDailyUsage > 0)
                .OrderByDescending(x => x.AverageDailyUsage)
                .ThenBy(x => x.AlertPriority)
                .FirstOrDefault();

            model.ClosestToReorderItem = filtered
                .Where(x => x.DaysUntilReorder.HasValue)
                .OrderBy(x => x.DaysUntilReorder!.Value)
                .ThenBy(x => x.AlertPriority)
                .FirstOrDefault();

            model.LargestSuggestedReorderItem = filtered
                .OrderByDescending(x => x.SuggestedReorderQuantity)
                .ThenBy(x => x.AlertPriority)
                .FirstOrDefault();

            model.TotalPages = model.TotalFilteredItems == 0
                ? 1
                : (int)Math.Ceiling(model.TotalFilteredItems / (double)pageSize);

            if (page > model.TotalPages)
                page = model.TotalPages;

            model.Page = page;

            var skip = (page - 1) * pageSize;
            model.Items = sorted.Skip(skip).Take(pageSize).ToList();

            model.StartItemNumber = model.TotalFilteredItems == 0 ? 0 : skip + 1;
            model.EndItemNumber = model.TotalFilteredItems == 0 ? 0 : skip + model.Items.Count;

            return View(model);
        }

        public async Task<IActionResult> ExportInventoryCsv(string? q)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var rows = await BuildInventoryReportQuery(scope, q)
                .OrderBy(x => x.ItemName)
                .ToListAsync();

            var csvRows = rows.Select(x => new[]
            {
                x.ItemName,
                x.SKU,
                x.CategoryName,
                x.PrimaryLocationName,
                x.TotalQuantityOnHand.ToString(),
                x.ReorderLevel.ToString(),
                x.LocationCount.ToString(),
                x.IsActive ? "Active" : "Inactive"
            });

            return BuildCsvFile(
                $"inventory-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
                new[]
                {
                    "Item Name",
                    "SKU",
                    "Category",
                    "Primary Location",
                    "Total Quantity On Hand",
                    "Reorder Level",
                    "Location Count",
                    "Status"
                },
                csvRows);
        }

        public async Task<IActionResult> ExportLowStockCsv(string? q)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var rows = await BuildLowStockQuery(scope, q)
                .OrderBy(x => x.TotalQuantityOnHand)
                .ThenBy(x => x.ItemName)
                .ToListAsync();

            var csvRows = rows.Select(x => new[]
            {
                x.ItemName,
                x.SKU,
                x.CategoryName,
                x.PrimaryLocationName,
                x.TotalQuantityOnHand.ToString(),
                x.ReorderLevel.ToString(),
                x.LocationCount.ToString(),
                x.IsActive ? "Active" : "Inactive"
            });

            return BuildCsvFile(
                $"low-stock-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
                new[]
                {
                    "Item Name",
                    "SKU",
                    "Category",
                    "Primary Location",
                    "Total Quantity On Hand",
                    "Reorder Level",
                    "Location Count",
                    "Status"
                },
                csvRows);
        }

        public async Task<IActionResult> ExportLocationSummaryCsv(string? q)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var rows = await BuildLocationSummaryQuery(scope, q)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var csvRows = rows.Select(x => new[]
            {
                x.Name,
                x.Building ?? "-",
                x.Room ?? "-",
                x.ActiveItemCount.ToString(),
                x.TotalUnits.ToString()
            });

            return BuildCsvFile(
                $"location-summary-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
                new[]
                {
                    "Location Name",
                    "Building",
                    "Room",
                    "Active Item Count",
                    "Total Units"
                },
                csvRows);
        }

        public async Task<IActionResult> ExportRecentTransactionsCsv(string? q, string actionFilter = "all")
        {
            var scope = await _accessService.GetScopeAsync(User);

            actionFilter = string.IsNullOrWhiteSpace(actionFilter)
                ? "all"
                : actionFilter.Trim().ToLowerInvariant();

            var rows = await BuildRecentTransactionsQuery(scope, q, actionFilter)
                .OrderByDescending(x => x.DateCreated)
                .ToListAsync();

            var csvRows = rows.Select(x => new[]
            {
                x.DateCreated.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                x.ItemName,
                x.SKU,
                x.ActionType,
                x.QuantityChange.ToString(),
                x.FromLocationName,
                x.ToLocationName,
                x.PerformedBy,
                x.Notes
            });

            return BuildCsvFile(
                $"recent-transactions-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
                new[]
                {
                    "Date",
                    "Item",
                    "SKU",
                    "Action",
                    "Quantity",
                    "From",
                    "To",
                    "Performed By",
                    "Notes"
                },
                csvRows);
        }

        public async Task<IActionResult> ExportAiInsightsCsv(
            string? q,
            int lookbackDays = 60,
            int targetCoverageDays = 21,
            int reorderSoonDays = 14,
            string alertFilter = "all",
            string categoryFilter = "all",
            string locationFilter = "all",
            string sortBy = "urgency")
        {
            var scope = await _accessService.GetScopeAsync(User);

            var model = await _inventoryAiService.BuildDashboardAsync(
                scope,
                q,
                lookbackDays,
                targetCoverageDays,
                reorderSoonDays);

            var filtered = FilterAiItems(model.Items, alertFilter, categoryFilter, locationFilter);
            var sorted = SortAiItems(filtered, sortBy);

            var csvRows = sorted.Select(x => new[]
            {
                x.ItemName,
                x.SKU,
                x.CategoryName,
                x.PrimaryLocationName,
                x.AlertLabel,
                x.ConfidenceLabel,
                x.CurrentQuantityOnHand.ToString(),
                x.ReorderLevel.ToString(),
                x.UnitsAboveReorderDisplay,
                x.AverageDailyUsageDisplay,
                x.DaysUntilReorderDisplay,
                x.PredictedReorderDateDisplay,
                x.SuggestedReorderQuantity.ToString(),
                x.RecommendedAction
            });

            return BuildCsvFile(
                $"ai-insights-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv",
                new[]
                {
                    "Item Name",
                    "SKU",
                    "Category",
                    "Visible Location",
                    "AI Status",
                    "Confidence",
                    "Current Quantity",
                    "Reorder Level",
                    "Gap To Reorder",
                    "Avg Daily Usage",
                    "Days Until Reorder",
                    "Predicted Reorder Date",
                    "Suggested Reorder Quantity",
                    "Recommended Action"
                },
                csvRows);
        }

        private static List<InventoryAiPredictionVM> FilterAiItems(
            List<InventoryAiPredictionVM> items,
            string? alertFilter,
            string? categoryFilter,
            string? locationFilter)
        {
            var filtered = items.AsEnumerable();

            alertFilter = string.IsNullOrWhiteSpace(alertFilter) ? "all" : alertFilter.Trim().ToLowerInvariant();
            categoryFilter = string.IsNullOrWhiteSpace(categoryFilter) ? "all" : categoryFilter.Trim();
            locationFilter = string.IsNullOrWhiteSpace(locationFilter) ? "all" : locationFilter.Trim();

            if (!string.Equals(alertFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.AlertLevel, alertFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(categoryFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.CategoryName, categoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(locationFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.PrimaryLocationName, locationFilter, StringComparison.OrdinalIgnoreCase));
            }

            return filtered.ToList();
        }

        private static List<InventoryAiPredictionVM> SortAiItems(
            List<InventoryAiPredictionVM> items,
            string? sortBy)
        {
            sortBy = string.IsNullOrWhiteSpace(sortBy) ? "urgency" : sortBy.Trim().ToLowerInvariant();

            return sortBy switch
            {
                "soonest" => items
                    .OrderBy(x => x.DaysUntilReorder ?? double.MaxValue)
                    .ThenBy(x => x.AlertPriority)
                    .ThenBy(x => x.ItemName)
                    .ToList(),

                "usage_desc" => items
                    .OrderByDescending(x => x.AverageDailyUsage)
                    .ThenBy(x => x.AlertPriority)
                    .ThenBy(x => x.ItemName)
                    .ToList(),

                "reorder_qty_desc" => items
                    .OrderByDescending(x => x.SuggestedReorderQuantity)
                    .ThenBy(x => x.AlertPriority)
                    .ThenBy(x => x.ItemName)
                    .ToList(),

                "stock_asc" => items
                    .OrderBy(x => x.CurrentQuantityOnHand)
                    .ThenBy(x => x.AlertPriority)
                    .ThenBy(x => x.ItemName)
                    .ToList(),

                "item_asc" => items
                    .OrderBy(x => x.ItemName)
                    .ThenBy(x => x.AlertPriority)
                    .ToList(),

                "category_asc" => items
                    .OrderBy(x => x.CategoryName)
                    .ThenBy(x => x.ItemName)
                    .ToList(),

                _ => items
                    .OrderBy(x => x.AlertPriority)
                    .ThenBy(x => x.DaysUntilReorder ?? double.MaxValue)
                    .ThenByDescending(x => x.AverageDailyUsage)
                    .ThenBy(x => x.ItemName)
                    .ToList()
            };
        }

        private IQueryable<InventoryReportRowVM> BuildInventoryReportQuery(AccessScope scope, string? q)
        {
            var items = _accessService.ApplyInventoryScope(_context.InventoryItems.AsNoTracking(), scope);
            var scopedStocks = _context.InventoryItemStocks.AsNoTracking();

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                scopedStocks = scopedStocks.Where(s => s.StorageLocationID == locationId);
            }

            var query =
                from item in items
                join stock in scopedStocks on item.ID equals stock.InventoryItemID into stockGroup
                let stockRowCount = stockGroup.Count()
                let totalQty = stockGroup.Sum(s => (int?)s.QuantityOnHand)
                let positiveLocationCount = stockGroup.Count(s => s.QuantityOnHand > 0)
                select new InventoryReportRowVM
                {
                    ItemId = item.ID,
                    ItemName = item.ItemName,
                    SKU = item.SKU,
                    CategoryName = item.Category != null ? item.Category.Name : "-",
                    PrimaryLocationName = scope.IsScopedUser
                        ? (scope.AssignedLocationName ?? "-")
                        : (item.StorageLocation != null ? item.StorageLocation.Name : "-"),
                    TotalQuantityOnHand = stockRowCount > 0 ? (totalQty ?? 0) : (scope.HasGlobalLocationAccess ? item.QuantityOnHand : (item.StorageLocationID == scope.AssignedLocationId ? item.QuantityOnHand : 0)),
                    ReorderLevel = item.ReorderLevel,
                    LocationCount = stockRowCount > 0 ? positiveLocationCount : ((scope.HasGlobalLocationAccess ? item.QuantityOnHand : (item.StorageLocationID == scope.AssignedLocationId ? item.QuantityOnHand : 0)) > 0 ? 1 : 0),
                    IsActive = item.IsActive
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();

                query = query.Where(x =>
                    x.ItemName.Contains(q) ||
                    x.SKU.Contains(q) ||
                    x.CategoryName.Contains(q) ||
                    x.PrimaryLocationName.Contains(q));
            }

            return query;
        }

        private IQueryable<LowStockReportRowVM> BuildLowStockQuery(AccessScope scope, string? q)
        {
            var items = _accessService.ApplyInventoryScope(_context.InventoryItems.AsNoTracking(), scope);
            var scopedStocks = _context.InventoryItemStocks.AsNoTracking();

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                scopedStocks = scopedStocks.Where(s => s.StorageLocationID == locationId);
            }

            var query =
                from item in items
                join stock in scopedStocks on item.ID equals stock.InventoryItemID into stockGroup
                let stockRowCount = stockGroup.Count()
                let totalQty = stockGroup.Sum(s => (int?)s.QuantityOnHand)
                let positiveLocationCount = stockGroup.Count(s => s.QuantityOnHand > 0)
                let fallbackQty = scope.HasGlobalLocationAccess ? item.QuantityOnHand : (item.StorageLocationID == scope.AssignedLocationId ? item.QuantityOnHand : 0)
                let effectiveQty = stockRowCount > 0 ? (totalQty ?? 0) : fallbackQty
                where effectiveQty <= item.ReorderLevel
                select new LowStockReportRowVM
                {
                    ItemId = item.ID,
                    ItemName = item.ItemName,
                    SKU = item.SKU,
                    CategoryName = item.Category != null ? item.Category.Name : "-",
                    PrimaryLocationName = scope.IsScopedUser
                        ? (scope.AssignedLocationName ?? "-")
                        : (item.StorageLocation != null ? item.StorageLocation.Name : "-"),
                    TotalQuantityOnHand = effectiveQty,
                    ReorderLevel = item.ReorderLevel,
                    LocationCount = stockRowCount > 0 ? positiveLocationCount : (fallbackQty > 0 ? 1 : 0),
                    IsActive = item.IsActive
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();

                query = query.Where(x =>
                    x.ItemName.Contains(q) ||
                    x.SKU.Contains(q) ||
                    x.CategoryName.Contains(q) ||
                    x.PrimaryLocationName.Contains(q));
            }

            return query;
        }

        private IQueryable<LocationStockSummaryRowVM> BuildLocationSummaryQuery(AccessScope scope, string? q)
        {
            var locations = _accessService.ApplyLocationScope(_context.StorageLocations.AsNoTracking(), scope);
            var scopedStocks = _context.InventoryItemStocks.AsNoTracking();

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                scopedStocks = scopedStocks.Where(s => s.StorageLocationID == locationId);
            }

            var query =
                from location in locations
                join stock in scopedStocks on location.ID equals stock.StorageLocationID into stockGroup
                select new LocationStockSummaryRowVM
                {
                    StorageLocationId = location.ID,
                    Name = location.Name,
                    Building = location.Building,
                    Room = location.Room,
                    ActiveItemCount = stockGroup.Count(x => x.QuantityOnHand > 0),
                    TotalUnits = stockGroup.Sum(x => (int?)x.QuantityOnHand) ?? 0
                };

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();

                query = query.Where(x =>
                    x.Name.Contains(q) ||
                    (x.Building != null && x.Building.Contains(q)) ||
                    (x.Room != null && x.Room.Contains(q)));
            }

            return query;
        }

        private IQueryable<RecentTransactionReportRowVM> BuildRecentTransactionsQuery(AccessScope scope, string? q, string actionFilter)
        {
            IQueryable<StockTransaction> query = _accessService.ApplyTransactionScope(_context.StockTransactions
                .AsNoTracking()
                .Include(t => t.InventoryItem)
                .Include(t => t.FromStorageLocation)
                .Include(t => t.ToStorageLocation), scope);

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                var searchLower = q.ToLowerInvariant();

                var searchCheckIn = searchLower == "checkin" || searchLower == "check in";
                var searchCheckOut = searchLower == "checkout" || searchLower == "check out";
                var searchAdjustment = searchLower == "adjustment" || searchLower == "adjust";
                var searchTransfer = searchLower == "transfer";

                query = query.Where(t =>
                    (t.InventoryItem != null && t.InventoryItem.ItemName.Contains(q)) ||
                    (t.InventoryItem != null && t.InventoryItem.SKU.Contains(q)) ||
                    (t.Notes != null && t.Notes.Contains(q)) ||
                    (t.PerformedBy != null && t.PerformedBy.Contains(q)) ||
                    (t.FromStorageLocation != null && t.FromStorageLocation.Name.Contains(q)) ||
                    (t.ToStorageLocation != null && t.ToStorageLocation.Name.Contains(q)) ||
                    (searchCheckIn && t.ActionType == StockActionType.CheckIn) ||
                    (searchCheckOut && t.ActionType == StockActionType.CheckOut) ||
                    (searchAdjustment && t.ActionType == StockActionType.Adjustment) ||
                    (searchTransfer && t.ActionType == StockActionType.Transfer));
            }

            query = actionFilter switch
            {
                "checkin" => query.Where(t => t.ActionType == StockActionType.CheckIn),
                "checkout" => query.Where(t => t.ActionType == StockActionType.CheckOut),
                "adjustment" => query.Where(t => t.ActionType == StockActionType.Adjustment),
                "transfer" => query.Where(t => t.ActionType == StockActionType.Transfer),
                _ => query
            };

            return query.Select(t => new RecentTransactionReportRowVM
            {
                TransactionId = t.ID,
                DateCreated = t.DateCreated,
                ItemName = t.InventoryItem != null ? t.InventoryItem.ItemName : "-",
                SKU = t.InventoryItem != null ? t.InventoryItem.SKU : "-",
                ActionType = t.ActionType.ToString(),
                QuantityChange = t.QuantityChange,
                FromLocationName = t.FromStorageLocation != null ? t.FromStorageLocation.Name : "-",
                ToLocationName = t.ToStorageLocation != null ? t.ToStorageLocation.Name : "-",
                PerformedBy = t.PerformedBy ?? "-",
                Notes = t.Notes ?? "-"
            });
        }

        private FileContentResult BuildCsvFile(string fileName, IEnumerable<string> headers, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();

            sb.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", row.Select(x => EscapeCsv(x ?? string.Empty))));
            }

            var bytes = Encoding.UTF8.GetBytes("\uFEFF" + sb.ToString());
            return File(bytes, "text/csv", fileName);
        }

        private static string EscapeCsv(string value)
        {
            if (value.Contains('"'))
                value = value.Replace("\"", "\"\"");

            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value}\"";

            return value;
        }
    }
}