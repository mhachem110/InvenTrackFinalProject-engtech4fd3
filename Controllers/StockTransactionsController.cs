using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
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

        // GET: StockTransactions/Create?inventoryItemId=5
        public async Task<IActionResult> Create(int inventoryItemId)
        {
            var item = await _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.StorageLocation)
                .FirstOrDefaultAsync(i => i.ID == inventoryItemId);

            if (item == null) return NotFound();

            var vm = new StockTransactionCreateVM
            {
                InventoryItemID = item.ID,
                ItemName = item.ItemName,
                SKU = item.SKU,
                CurrentQuantityOnHand = item.QuantityOnHand,
                CurrentLocationID = item.StorageLocationID,
                CurrentLocationName = item.StorageLocation?.Name ?? "-",
                ActionType = StockActionType.CheckIn,
                Quantity = 1
            };

            vm.LocationOptions = await BuildLocationOptionsAsync(item.StorageLocationID);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StockTransactionCreateVM vm)
        {
            vm.LocationOptions = await BuildLocationOptionsAsync(vm.CurrentLocationID);

            if (!ModelState.IsValid)
                return View(vm);

            var (ok, error) = await _stockService.ApplyTransactionAsync(
                inventoryItemId: vm.InventoryItemID,
                actionType: vm.ActionType,
                quantity: vm.Quantity,
                targetLocationId: vm.ActionType == StockActionType.Transfer ? vm.TargetLocationID : null,
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

        private async Task<IEnumerable<SelectListItem>> BuildLocationOptionsAsync(int currentLocationId)
        {
            var locations = await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();

            return locations
                .Where(l => l.ID != currentLocationId)
                .Select(l => new SelectListItem
                {
                    Value = l.ID.ToString(),
                    Text = l.Name
                })
                .ToList();
        }
    }
}