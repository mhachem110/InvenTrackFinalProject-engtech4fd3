using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.Utilities;
using InvenTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin,RegionalManager,Manager,Supervisor,Employee")]
    public class StockTransactionsController : Controller
    {
        private readonly InvenTrackContext _context;
        private readonly StockService _stockService;
        private readonly AppAccessService _accessService;

        public StockTransactionsController(
            InvenTrackContext context,
            StockService stockService,
            AppAccessService accessService)
        {
            _context = context;
            _stockService = stockService;
            _accessService = accessService;
        }

        public async Task<IActionResult> Index(
            string? searchString,
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

            IQueryable<StockTransaction> query = _context.StockTransactions
                .AsNoTracking()
                .Include(t => t.InventoryItem)
                .Include(t => t.FromStorageLocation)
                .Include(t => t.ToStorageLocation);

            query = _accessService.ApplyTransactionScope(query, scope);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                var searchLower = searchString.ToLowerInvariant();

                var searchCheckIn = searchLower == "checkin" || searchLower == "check in";
                var searchCheckOut = searchLower == "checkout" || searchLower == "check out";
                var searchAdjustment = searchLower == "adjustment" || searchLower == "adjust";
                var searchTransfer = searchLower == "transfer";

                query = query.Where(t =>
                    (t.InventoryItem != null && t.InventoryItem.ItemName.Contains(searchString)) ||
                    (t.InventoryItem != null && t.InventoryItem.SKU.Contains(searchString)) ||
                    (t.Notes != null && t.Notes.Contains(searchString)) ||
                    (t.PerformedBy != null && t.PerformedBy.Contains(searchString)) ||
                    (t.FromStorageLocation != null && t.FromStorageLocation.Name.Contains(searchString)) ||
                    (t.ToStorageLocation != null && t.ToStorageLocation.Name.Contains(searchString)) ||
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

            var model = await PaginatedList<StockTransaction>.CreateAsync(
                query.OrderByDescending(t => t.DateCreated),
                page,
                pageSize);

            if (model.TotalPages > 0 && page > model.TotalPages)
            {
                page = model.TotalPages;

                model = await PaginatedList<StockTransaction>.CreateAsync(
                    query.OrderByDescending(t => t.DateCreated),
                    page,
                    pageSize);
            }

            ViewData["CurrentFilter"] = searchString ?? string.Empty;
            ViewData["CurrentAction"] = actionFilter;
            ViewData["CurrentPageSize"] = pageSize;

            return View(model);
        }

        [Authorize(Roles = "Admin,RegionalManager,Manager,Supervisor")]
        public async Task<IActionResult> Create(int inventoryItemId)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var item = await GetScopedItemAsync(inventoryItemId, scope);
            if (item == null)
                return NotFound();

            var allowTransfer = scope.CanConfirmTransfers;

            var vm = await BuildCreateViewModelAsync(item, scope, allowTransfer);
            return View(vm);
        }

        [Authorize(Roles = "Admin,RegionalManager,Manager,Supervisor")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockTransactionCreateVM vm)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var item = await GetScopedItemAsync(vm.InventoryItemID, scope);
            if (item == null)
                return NotFound();

            vm.AllowTransfer = scope.CanConfirmTransfers;

            var rebuiltVm = await BuildCreateViewModelAsync(item, scope, vm.AllowTransfer);

            vm.ItemName = rebuiltVm.ItemName;
            vm.SKU = rebuiltVm.SKU;
            vm.TotalQuantityOnHand = rebuiltVm.TotalQuantityOnHand;
            vm.Locations = rebuiltVm.Locations;

            ValidateScopedTransaction(vm, scope);

            if (!ModelState.IsValid)
                return View(vm);

            int? fromId = null;
            int? toId = null;

            if (vm.ActionType == StockActionType.CheckIn)
            {
                toId = vm.ToLocationID;
            }
            else if (vm.ActionType == StockActionType.CheckOut)
            {
                fromId = vm.FromLocationID;
            }
            else if (vm.ActionType == StockActionType.Adjustment)
            {
                fromId = vm.FromLocationID;
            }
            else if (vm.ActionType == StockActionType.Transfer)
            {
                fromId = vm.FromLocationID;
                toId = vm.ToLocationID;
            }

            var performedBy = string.IsNullOrWhiteSpace(vm.PerformedBy)
                ? (User.Identity?.Name ?? "System")
                : vm.PerformedBy.Trim();

            var (ok, error) = await _stockService.ApplyTransactionAsync(
                inventoryItemId: vm.InventoryItemID,
                actionType: vm.ActionType,
                quantity: vm.Quantity,
                fromLocationId: fromId,
                toLocationId: toId,
                notes: vm.Notes,
                performedBy: performedBy
            );

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to apply transaction.");
                return View(vm);
            }

            return RedirectToAction("Details", "InventoryItems", new { id = vm.InventoryItemID });
        }

        private async Task<InventoryItem?> GetScopedItemAsync(int inventoryItemId, AccessScope scope)
        {
            IQueryable<InventoryItem> query = _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.StorageLocation)
                .Include(i => i.InventoryItemStocks)
                    .ThenInclude(s => s.StorageLocation);

            query = _accessService.ApplyInventoryScope(query, scope);

            return await query.FirstOrDefaultAsync(i => i.ID == inventoryItemId);
        }

        private async Task<StockTransactionCreateVM> BuildCreateViewModelAsync(
            InventoryItem item,
            AccessScope scope,
            bool allowTransfer)
        {
            var stockRows = await _context.InventoryItemStocks
                .AsNoTracking()
                .Where(s => s.InventoryItemID == item.ID)
                .ToListAsync();

            var stockByLocation = stockRows
                .GroupBy(s => s.StorageLocationID)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityOnHand));

            List<StorageLocation> locationPool;

            if (scope.HasGlobalLocationAccess)
            {
                locationPool = await _context.StorageLocations
                    .AsNoTracking()
                    .OrderBy(l => l.Name)
                    .ToListAsync();
            }
            else if (allowTransfer)
            {
                locationPool = await _context.StorageLocations
                    .AsNoTracking()
                    .OrderBy(l => l.Name)
                    .ToListAsync();
            }
            else
            {
                var assignedLocationId = scope.AssignedLocationId ?? 0;

                locationPool = await _context.StorageLocations
                    .AsNoTracking()
                    .Where(l => l.ID == assignedLocationId)
                    .OrderBy(l => l.Name)
                    .ToListAsync();
            }

            var locations = locationPool.Select(l => new LocationStockVM
            {
                LocationID = l.ID,
                LocationName = l.Name,
                QuantityOnHand = GetScopedQuantityForLocation(item, stockByLocation, scope, l.ID)
            }).ToList();

            var visibleTotal = scope.HasGlobalLocationAccess
                ? GetGlobalTotalQuantity(item, stockByLocation)
                : GetScopedQuantityForLocation(item, stockByLocation, scope, scope.AssignedLocationId ?? 0);

            int? defaultFrom;
            int? defaultTo;

            if (scope.HasGlobalLocationAccess)
            {
                defaultFrom = locations
                    .Where(x => x.QuantityOnHand > 0)
                    .OrderByDescending(x => x.QuantityOnHand)
                    .Select(x => (int?)x.LocationID)
                    .FirstOrDefault();

                defaultTo = item.StorageLocationID;
            }
            else
            {
                var assignedLocationId = scope.AssignedLocationId;
                var assignedQty = assignedLocationId.HasValue
                    ? locations.FirstOrDefault(x => x.LocationID == assignedLocationId.Value)?.QuantityOnHand ?? 0
                    : 0;

                defaultFrom = assignedQty > 0 ? assignedLocationId : null;
                defaultTo = assignedLocationId;
            }

            return new StockTransactionCreateVM
            {
                InventoryItemID = item.ID,
                ItemName = item.ItemName,
                SKU = item.SKU,
                TotalQuantityOnHand = visibleTotal,
                ActionType = StockActionType.CheckIn,
                Quantity = 1,
                FromLocationID = defaultFrom,
                ToLocationID = defaultTo,
                Locations = locations,
                AllowTransfer = allowTransfer
            };
        }

        private int GetGlobalTotalQuantity(InventoryItem item, Dictionary<int, int> stockByLocation)
        {
            if (stockByLocation.Count > 0)
                return stockByLocation.Values.Sum();

            return item.QuantityOnHand;
        }

        private int GetScopedQuantityForLocation(
            InventoryItem item,
            Dictionary<int, int> stockByLocation,
            AccessScope scope,
            int locationId)
        {
            if (locationId <= 0)
                return 0;

            if (!scope.HasGlobalLocationAccess && scope.AssignedLocationId != locationId)
                return 0;

            if (stockByLocation.TryGetValue(locationId, out var qty))
                return qty;

            if (item.StorageLocationID == locationId)
                return item.QuantityOnHand;

            return 0;
        }

        private void ValidateScopedTransaction(StockTransactionCreateVM vm, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return;

            var assignedLocationId = scope.AssignedLocationId;

            if (!assignedLocationId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "Your account is not assigned to a location.");
                return;
            }

            if (!vm.AllowTransfer && vm.ActionType == StockActionType.Transfer)
            {
                ModelState.AddModelError(nameof(vm.ActionType),
                    "Your role cannot directly create transfer transactions. Please use a transfer request.");
            }

            if (vm.ActionType == StockActionType.CheckIn)
            {
                if (vm.ToLocationID.HasValue && vm.ToLocationID.Value != assignedLocationId.Value)
                {
                    ModelState.AddModelError(nameof(vm.ToLocationID),
                        "You can only check inventory into your assigned location.");
                }
            }

            if (vm.ActionType == StockActionType.CheckOut || vm.ActionType == StockActionType.Adjustment)
            {
                if (vm.FromLocationID.HasValue && vm.FromLocationID.Value != assignedLocationId.Value)
                {
                    ModelState.AddModelError(nameof(vm.FromLocationID),
                        "You can only use your assigned location for this action.");
                }
            }

            if (vm.ActionType == StockActionType.Transfer)
            {
                if (vm.FromLocationID.HasValue && vm.FromLocationID.Value != assignedLocationId.Value)
                {
                    ModelState.AddModelError(nameof(vm.FromLocationID),
                        "Transfers must originate from your assigned location.");
                }
            }
        }
    }
}