using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.ViewModels.TransferRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.RegionalManager},{AppRoles.Manager},{AppRoles.Supervisor},{AppRoles.Employee}")]
    public class StockTransferRequestsController : Controller
    {
        private readonly InvenTrackContext _context;
        private readonly StockService _stockService;
        private readonly AppAccessService _accessService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly TransferRequestNotificationService _notificationService;

        public StockTransferRequestsController(
            InvenTrackContext context,
            StockService stockService,
            AppAccessService accessService,
            UserManager<ApplicationUser> userManager,
            TransferRequestNotificationService notificationService)
        {
            _context = context;
            _stockService = stockService;
            _accessService = accessService;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var scope = await _accessService.GetScopeAsync(User);

            var query = _context.StockTransferRequests
                .AsNoTracking()
                .Include(r => r.InventoryItem)
                .Include(r => r.FromStorageLocation)
                .Include(r => r.ToStorageLocation)
                .AsQueryable();

            query = _accessService.ApplyTransferRequestScope(query, scope);

            var rows = await query
                .OrderBy(r => r.Status == TransferRequestStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.DateRequested)
                .ToListAsync();

            return View(rows);
        }

        [HttpGet]
        public async Task<IActionResult> BadgeCount()
        {
            var scope = await _accessService.GetScopeAsync(User);

            var query = _context.StockTransferRequests
                .AsNoTracking()
                .Where(r => r.Status == TransferRequestStatus.Pending);

            query = _accessService.ApplyTransferRequestScope(query, scope);

            var count = await query.CountAsync();

            return Json(new { count });
        }

        public async Task<IActionResult> Create(int inventoryItemId)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (!scope.CanRequestTransfers)
                return Forbid();

            var item = await _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.StorageLocation)
                .FirstOrDefaultAsync(i => i.ID == inventoryItemId);

            if (item == null)
                return NotFound();

            if (scope.IsScopedUser)
            {
                var hasAccess = item.StorageLocationID == scope.AssignedLocationId ||
                                await _context.InventoryItemStocks.AnyAsync(s =>
                                    s.InventoryItemID == item.ID &&
                                    s.StorageLocationID == scope.AssignedLocationId);

                if (!hasAccess)
                    return Forbid();
            }

            var locations = await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.Name
                })
                .ToListAsync();

            var vm = new TransferRequestCreateVM
            {
                InventoryItemID = item.ID,
                ItemName = item.ItemName,
                SKU = item.SKU,
                FromStorageLocationID = scope.AssignedLocationId ?? item.StorageLocationID,
                AvailableLocations = locations
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TransferRequestCreateVM vm)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (!scope.CanRequestTransfers)
                return Forbid();

            vm.AvailableLocations = await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new SelectListItem
                {
                    Value = x.ID.ToString(),
                    Text = x.Name
                })
                .ToListAsync();

            var item = await _context.InventoryItems
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ID == vm.InventoryItemID);

            if (item == null)
                return NotFound();

            vm.ItemName = item.ItemName;
            vm.SKU = item.SKU;

            if (scope.IsScopedUser && vm.FromStorageLocationID != scope.AssignedLocationId)
            {
                ModelState.AddModelError(nameof(vm.FromStorageLocationID), "You can only request transfers from your assigned location.");
            }

            if (vm.FromStorageLocationID == vm.ToStorageLocationID)
            {
                ModelState.AddModelError(nameof(vm.ToStorageLocationID), "Destination must be different from source.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();

            var request = new StockTransferRequest
            {
                InventoryItemID = vm.InventoryItemID,
                Quantity = vm.Quantity,
                FromStorageLocationID = vm.FromStorageLocationID,
                ToStorageLocationID = vm.ToStorageLocationID,
                Notes = vm.Notes,
                Status = TransferRequestStatus.Pending,
                RequestedByUserId = currentUser.Id,
                RequestedByName = currentUser.UserName ?? currentUser.Email ?? "Unknown",
                DateRequested = DateTime.UtcNow
            };

            _context.StockTransferRequests.Add(request);
            await _context.SaveChangesAsync();

            await _notificationService.NotifyTransferRequestChangedAsync(
                request,
                $"New transfer request submitted for {vm.ItemName}.");

            TempData["TransferMsg"] = "Transfer request submitted.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.RegionalManager},{AppRoles.Manager}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (!scope.CanConfirmTransfers)
                return Forbid();

            var request = await _context.StockTransferRequests
                .Include(r => r.InventoryItem)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (request == null)
                return NotFound();

            if (request.Status != TransferRequestStatus.Pending)
            {
                TempData["TransferMsg"] = "Only pending requests can be approved.";
                return RedirectToAction(nameof(Index));
            }

            if (scope.IsScopedUser &&
                request.FromStorageLocationID != scope.AssignedLocationId &&
                request.ToStorageLocationID != scope.AssignedLocationId)
            {
                return Forbid();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();

            var (ok, error) = await _stockService.ApplyTransactionAsync(
                inventoryItemId: request.InventoryItemID,
                actionType: StockActionType.Transfer,
                quantity: request.Quantity,
                fromLocationId: request.FromStorageLocationID,
                toLocationId: request.ToStorageLocationID,
                notes: $"Approved transfer request #{request.ID}. {request.Notes}",
                performedBy: currentUser.UserName ?? currentUser.Email ?? "Unknown");

            if (!ok)
            {
                TempData["TransferMsg"] = error ?? "Unable to approve request.";
                return RedirectToAction(nameof(Index));
            }

            request.Status = TransferRequestStatus.Approved;
            request.ReviewedByUserId = currentUser.Id;
            request.ReviewedByName = currentUser.UserName ?? currentUser.Email ?? "Unknown";
            request.DateReviewed = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _notificationService.NotifyTransferRequestChangedAsync(
                request,
                $"Transfer request #{request.ID} was approved.");

            TempData["TransferMsg"] = "Transfer request approved.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = $"{AppRoles.Admin},{AppRoles.RegionalManager},{AppRoles.Manager}")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var request = await _context.StockTransferRequests.FirstOrDefaultAsync(r => r.ID == id);
            if (request == null)
                return NotFound();

            if (request.Status != TransferRequestStatus.Pending)
            {
                TempData["TransferMsg"] = "Only pending requests can be rejected.";
                return RedirectToAction(nameof(Index));
            }

            if (scope.IsScopedUser &&
                request.FromStorageLocationID != scope.AssignedLocationId &&
                request.ToStorageLocationID != scope.AssignedLocationId)
            {
                return Forbid();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Challenge();

            request.Status = TransferRequestStatus.Rejected;
            request.ReviewedByUserId = currentUser.Id;
            request.ReviewedByName = currentUser.UserName ?? currentUser.Email ?? "Unknown";
            request.DateReviewed = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _notificationService.NotifyTransferRequestChangedAsync(
                request,
                $"Transfer request #{request.ID} was rejected.");

            TempData["TransferMsg"] = "Transfer request rejected.";
            return RedirectToAction(nameof(Index));
        }
    }
}