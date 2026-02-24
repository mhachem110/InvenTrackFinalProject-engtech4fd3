using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin,Manager,Viewer")]
    public class StockTransactionsController : Controller
    {
        private readonly InvenTrackContext _context;
        private readonly StockService _stockService;

        public StockTransactionsController(InvenTrackContext context, StockService stockService)
        {
            _context = context;
            _stockService = stockService;
        }

        public async Task<IActionResult> Index()
        {
            var txs = await _context.StockTransactions
                .AsNoTracking()
                .Include(t => t.InventoryItem)
                .Include(t => t.FromStorageLocation)
                .Include(t => t.ToStorageLocation)
                .OrderByDescending(t => t.DateCreated)
                .Take(250)
                .ToListAsync();

            return View(txs);
        }
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(int inventoryItemId)
        {
            var item = await _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.StorageLocation)
                .FirstOrDefaultAsync(i => i.ID == inventoryItemId);

            if (item == null) return NotFound();

            var hasStocks = await _context.InventoryItemStocks.AnyAsync(s => s.InventoryItemID == inventoryItemId);
            List<InventoryItemStock> stocks;

            if (hasStocks)
            {
                stocks = await _context.InventoryItemStocks
                    .AsNoTracking()
                    .Include(s => s.StorageLocation)
                    .Where(s => s.InventoryItemID == inventoryItemId)
                    .ToListAsync();
            }
            else
            {
                stocks = new List<InventoryItemStock>
                {
                    new InventoryItemStock
                    {
                        InventoryItemID = inventoryItemId,
                        StorageLocationID = item.StorageLocationID,
                        StorageLocation = item.StorageLocation ?? new StorageLocation { Name = "-" },
                        QuantityOnHand = item.QuantityOnHand
                    }
                };
            }

            var allLocations = await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();

            var locList = allLocations.Select(l =>
            {
                var s = stocks.FirstOrDefault(x => x.StorageLocationID == l.ID);
                return new LocationStockVM
                {
                    LocationID = l.ID,
                    LocationName = l.Name,
                    QuantityOnHand = s?.QuantityOnHand ?? 0
                };
            }).ToList();

            var defaultFrom = locList.OrderByDescending(x => x.QuantityOnHand).FirstOrDefault(x => x.QuantityOnHand > 0)?.LocationID;

            var vm = new StockTransactionCreateVM
            {
                InventoryItemID = item.ID,
                ItemName = item.ItemName,
                SKU = item.SKU,
                TotalQuantityOnHand = locList.Sum(x => x.QuantityOnHand),
                ActionType = StockActionType.CheckIn,
                Quantity = 1,
                FromLocationID = defaultFrom,
                ToLocationID = null,
                Locations = locList
            };

            return View(vm);
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockTransactionCreateVM vm)
        {
            var allLocations = await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();

            var stocks = await _context.InventoryItemStocks
                .AsNoTracking()
                .Where(s => s.InventoryItemID == vm.InventoryItemID)
                .ToListAsync();

            vm.Locations = allLocations.Select(l =>
            {
                var s = stocks.FirstOrDefault(x => x.StorageLocationID == l.ID);
                return new LocationStockVM
                {
                    LocationID = l.ID,
                    LocationName = l.Name,
                    QuantityOnHand = s?.QuantityOnHand ?? 0
                };
            }).ToList();

            vm.TotalQuantityOnHand = vm.Locations.Sum(x => x.QuantityOnHand);

            if (!ModelState.IsValid)
                return View(vm);

            int? fromId = null;
            int? toId = null;

            if (vm.ActionType == StockActionType.CheckIn)
                toId = vm.ToLocationID;
            else if (vm.ActionType == StockActionType.CheckOut)
                fromId = vm.FromLocationID;
            else if (vm.ActionType == StockActionType.Adjustment)
                fromId = vm.FromLocationID;
            else if (vm.ActionType == StockActionType.Transfer)
            {
                fromId = vm.FromLocationID;
                toId = vm.ToLocationID;
            }

            var (ok, error) = await _stockService.ApplyTransactionAsync(
                inventoryItemId: vm.InventoryItemID,
                actionType: vm.ActionType,
                quantity: vm.Quantity,
                fromLocationId: fromId,
                toLocationId: toId,
                notes: vm.Notes,
                performedBy: vm.PerformedBy
            );

            if (!ok)
            {
                ModelState.AddModelError(string.Empty, error ?? "Unable to apply transaction.");
                return View(vm);
            }

            return RedirectToAction("Details", "InventoryItems", new { id = vm.InventoryItemID });
        }
    }
}