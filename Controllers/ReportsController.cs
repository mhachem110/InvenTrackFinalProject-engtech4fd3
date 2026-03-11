using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Utilities;
using InvenTrack.ViewModels.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin,Manager,Viewer")]
    public class ReportsController : Controller
    {
        private readonly InvenTrackContext _context;

        public ReportsController(InvenTrackContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new ReportIndexVM
            {
                InventoryItemCount = await _context.InventoryItems.AsNoTracking().CountAsync(),
                StorageLocationCount = await _context.StorageLocations.AsNoTracking().CountAsync(),
                RecentTransactionCount = await _context.StockTransactions.AsNoTracking().CountAsync(),
                LowStockItemCount = await BuildLowStockQuery(null).CountAsync()
            };

            return View(vm);
        }

        public async Task<IActionResult> LowStock(string? q, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            var query = BuildLowStockQuery(q);

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
            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            var query = BuildLocationSummaryQuery(q);

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
            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            actionFilter = string.IsNullOrWhiteSpace(actionFilter)
                ? "all"
                : actionFilter.Trim().ToLowerInvariant();

            var query = BuildRecentTransactionsQuery(q, actionFilter);

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

        public async Task<IActionResult> ExportInventoryCsv(string? q)
        {
            var rows = await BuildInventoryReportQuery(q)
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
            var rows = await BuildLowStockQuery(q)
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
            var rows = await BuildLocationSummaryQuery(q)
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
            actionFilter = string.IsNullOrWhiteSpace(actionFilter)
                ? "all"
                : actionFilter.Trim().ToLowerInvariant();

            var rows = await BuildRecentTransactionsQuery(q, actionFilter)
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

        private IQueryable<InventoryReportRowVM> BuildInventoryReportQuery(string? q)
        {
            var query =
                from item in _context.InventoryItems.AsNoTracking()
                join stock in _context.InventoryItemStocks.AsNoTracking()
                    on item.ID equals stock.InventoryItemID into stockGroup
                let stockRowCount = stockGroup.Count()
                let totalQty = stockGroup.Sum(s => (int?)s.QuantityOnHand)
                let positiveLocationCount = stockGroup.Count(s => s.QuantityOnHand > 0)
                select new InventoryReportRowVM
                {
                    ItemId = item.ID,
                    ItemName = item.ItemName,
                    SKU = item.SKU,
                    CategoryName = item.Category != null ? item.Category.Name : "-",
                    PrimaryLocationName = item.StorageLocation != null ? item.StorageLocation.Name : "-",
                    TotalQuantityOnHand = stockRowCount > 0 ? (totalQty ?? 0) : item.QuantityOnHand,
                    ReorderLevel = item.ReorderLevel,
                    LocationCount = stockRowCount > 0 ? positiveLocationCount : (item.QuantityOnHand > 0 ? 1 : 0),
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

        private IQueryable<LowStockReportRowVM> BuildLowStockQuery(string? q)
        {
            var query =
                from item in _context.InventoryItems.AsNoTracking()
                join stock in _context.InventoryItemStocks.AsNoTracking()
                    on item.ID equals stock.InventoryItemID into stockGroup
                let stockRowCount = stockGroup.Count()
                let totalQty = stockGroup.Sum(s => (int?)s.QuantityOnHand)
                let positiveLocationCount = stockGroup.Count(s => s.QuantityOnHand > 0)
                let effectiveQty = stockRowCount > 0 ? (totalQty ?? 0) : item.QuantityOnHand
                where effectiveQty <= item.ReorderLevel
                select new LowStockReportRowVM
                {
                    ItemId = item.ID,
                    ItemName = item.ItemName,
                    SKU = item.SKU,
                    CategoryName = item.Category != null ? item.Category.Name : "-",
                    PrimaryLocationName = item.StorageLocation != null ? item.StorageLocation.Name : "-",
                    TotalQuantityOnHand = effectiveQty,
                    ReorderLevel = item.ReorderLevel,
                    LocationCount = stockRowCount > 0 ? positiveLocationCount : (item.QuantityOnHand > 0 ? 1 : 0),
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

        private IQueryable<LocationStockSummaryRowVM> BuildLocationSummaryQuery(string? q)
        {
            var query =
                from location in _context.StorageLocations.AsNoTracking()
                join stock in _context.InventoryItemStocks.AsNoTracking()
                    on location.ID equals stock.StorageLocationID into stockGroup
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

        private IQueryable<RecentTransactionReportRowVM> BuildRecentTransactionsQuery(string? q, string actionFilter)
        {
            IQueryable<StockTransaction> query = _context.StockTransactions
                .AsNoTracking()
                .Include(t => t.InventoryItem)
                .Include(t => t.FromStorageLocation)
                .Include(t => t.ToStorageLocation);

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
                    (searchTransfer && t.ActionType == StockActionType.Transfer)
                );
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