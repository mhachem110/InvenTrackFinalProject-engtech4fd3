using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.ViewModels.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager + "," + AppRoles.Manager + "," + AppRoles.Supervisor + "," + AppRoles.Employee)]
    public class OrderRequestsController : Controller
    {
        private readonly OrderService _orderService;
        private readonly AppAccessService _accessService;

        public OrderRequestsController(OrderService orderService, AppAccessService accessService)
        {
            _orderService = orderService;
            _accessService = accessService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? status)
        {
            var vm = await _orderService.BuildIndexVmAsync(User, search, status);
            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int inventoryItemId)
        {
            var vm = await _orderService.BuildCreateVmAsync(inventoryItemId, User);
            if (vm == null) return NotFound();
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReorderRequestCreateVM vm)
        {
            var rebuilt = await _orderService.BuildCreateVmAsync(vm.InventoryItemID, User);
            if (rebuilt == null) return NotFound();
            rebuilt.RequestedQuantity = vm.RequestedQuantity;
            rebuilt.DestinationStorageLocationID = vm.DestinationStorageLocationID;
            rebuilt.RelatedLocationIds = vm.RelatedLocationIds ?? new List<int>();
            rebuilt.Notes = vm.Notes;
            vm = rebuilt;

            if (!ModelState.IsValid)
                return View(vm);

            var (ok, error, orderId) = await _orderService.SubmitAsync(vm, User);
            if (!ok)
            {
                ModelState.AddModelError(string.Empty, error ?? "Could not submit reorder.");
                return View(vm);
            }

            TempData["OrderMsg"] = vm.RequiresApproval
                ? $"Restock request #{orderId} submitted for approval."
                : $"Restock order #{orderId} completed and stock updated.";

            return RedirectToAction("Details", "InventoryItems", new { id = vm.InventoryItemID });
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager + "," + AppRoles.Manager)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var (ok, error) = await _orderService.ApproveAsync(id, User);
            TempData["OrderMsg"] = ok ? "Restock request approved and stock added." : error;
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = AppRoles.Admin + "," + AppRoles.RegionalManager + "," + AppRoles.Manager)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var (ok, error) = await _orderService.RejectAsync(id, User);
            TempData["OrderMsg"] = ok ? "Restock request rejected." : error;
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> BadgeCount()
        {
            var scope = await _accessService.GetScopeAsync(User);
            var count = await _orderService.PendingCountAsync(scope);
            return Json(new { count });
        }
    }
}
