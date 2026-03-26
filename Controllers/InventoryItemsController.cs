using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Services;
using InvenTrack.Utilities;
using InvenTrack.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles =
        AppRoles.Admin + "," +
        AppRoles.RegionalManager + "," +
        AppRoles.Manager + "," +
        AppRoles.Supervisor + "," +
        AppRoles.Employee)]
    public class InventoryItemsController : Controller
    {
        private readonly InvenTrackContext _context;
        private readonly AppAccessService _accessService;
        private readonly InventoryAiService _inventoryAiService;
        private readonly OrderService _orderService;
        private readonly BarcodeService _barcodeService;

        public InventoryItemsController(
            InvenTrackContext context,
            AppAccessService accessService,
            InventoryAiService inventoryAiService,
            OrderService orderService,
            BarcodeService barcodeService)
        {
            _context = context;
            _accessService = accessService;
            _inventoryAiService = inventoryAiService;
            _orderService = orderService;
            _barcodeService = barcodeService;
        }

        private static string NormalizeSku(string? sku)
            => (sku ?? string.Empty).Trim();

        private async Task<bool> SkuExistsAsync(string sku, int? excludeId = null)
        {
            sku = NormalizeSku(sku);
            if (string.IsNullOrWhiteSpace(sku)) return false;

            return await _context.InventoryItems.AnyAsync(i =>
                i.SKU == sku && (!excludeId.HasValue || i.ID != excludeId.Value));
        }

        private IQueryable<InventoryItem> ApplyInventoryScope(IQueryable<InventoryItem> query, AccessScope scope)
        {
            if (scope.HasGlobalLocationAccess)
                return query;

            var locationId = scope.AssignedLocationId!.Value;

            return query.Where(i =>
                i.StorageLocationID == locationId ||
                _context.InventoryItemStocks.Any(s =>
                    s.InventoryItemID == i.ID &&
                    s.StorageLocationID == locationId));
        }

        private async Task PopulateDropDownsAsync(
            AccessScope scope,
            int? categoryID = null,
            int? locationID = null)
        {
            ViewData["CategoryID"] = new SelectList(
                await _context.Categories
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                "ID",
                "Name",
                categoryID);

            IQueryable<StorageLocation> locationsQuery = _context.StorageLocations
                .AsNoTracking();

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                locationsQuery = locationsQuery.Where(l => l.ID == locationId);
            }

            ViewData["StorageLocationID"] = new SelectList(
                await locationsQuery.OrderBy(l => l.Name).ToListAsync(),
                "ID",
                "Name",
                locationID);
        }

        private async Task<byte[]?> ResizeWebpWithTimeoutAsync(byte[] bytes, int w, int h, int timeoutMs)
        {
            var resizeTask = Task.Run(() => ResizeImage.shrinkImageWebp(bytes, w, h));
            var done = await Task.WhenAny(resizeTask, Task.Delay(timeoutMs));
            if (done != resizeTask) return null;
            return await resizeTask;
        }

        private async Task<string?> TryAddPictureAsync(InventoryItem item, IFormFile? thePicture)
        {
            if (thePicture == null || thePicture.Length == 0)
                return null;

            if (string.IsNullOrWhiteSpace(thePicture.ContentType) || !thePicture.ContentType.StartsWith("image/"))
                return "Please upload a valid image file (JPG/PNG/WEBP).";

            const long maxBytes = 5 * 1024 * 1024;
            if (thePicture.Length > maxBytes)
                return "Image is too large. Please upload an image under 5 MB.";

            byte[] pictureArray;
            try
            {
                using var ms = new MemoryStream();
                await thePicture.CopyToAsync(ms);
                pictureArray = ms.ToArray();
            }
            catch
            {
                return "Could not read the uploaded file. Please try again.";
            }

            try
            {
                const int fullTimeoutMs = 2500;
                const int thumbTimeoutMs = 1500;

                var full = await ResizeWebpWithTimeoutAsync(pictureArray, 500, 600, fullTimeoutMs);
                if (full == null) return "Image processing took too long. Try a smaller image.";

                var thumb = await ResizeWebpWithTimeoutAsync(pictureArray, 100, 120, thumbTimeoutMs);
                if (thumb == null) return "Thumbnail processing took too long. Try a smaller image.";

                item.ItemPhoto ??= new ItemPhoto();
                item.ItemThumbnail ??= new ItemThumbnail();

                item.ItemPhoto.InventoryItem = item;
                item.ItemThumbnail.InventoryItem = item;

                item.ItemPhoto.MimeType = "image/webp";
                item.ItemThumbnail.MimeType = "image/webp";

                item.ItemPhoto.Content = full;
                item.ItemThumbnail.Content = thumb;

                return null;
            }
            catch
            {
                item.ItemPhoto = null;
                item.ItemThumbnail = null;
                return "Image processing failed. Try a different JPG/PNG image.";
            }
        }

        public async Task<IActionResult> Index(
            string? searchString,
            string statusFilter = "all",
            int page = 1,
            int pageSize = 10)
        {
            var scope = await _accessService.GetScopeAsync(User);

            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            statusFilter = string.IsNullOrWhiteSpace(statusFilter)
                ? "all"
                : statusFilter.Trim().ToLowerInvariant();

            IQueryable<InventoryItem> query = _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.ItemThumbnail)
                .Include(i => i.Category)
                .Include(i => i.StorageLocation);

            query = ApplyInventoryScope(query, scope);

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                query = query.Where(i =>
                    i.ItemName.Contains(searchString) ||
                    i.SKU.Contains(searchString) ||
                    (i.Description != null && i.Description.Contains(searchString)) ||
                    i.Barcode.Contains(searchString) ||
                    (i.Category != null && i.Category.Name.Contains(searchString)) ||
                    (i.StorageLocation != null && i.StorageLocation.Name.Contains(searchString)));
            }

            query = statusFilter switch
            {
                "active" => query.Where(i => i.IsActive),
                "inactive" => query.Where(i => !i.IsActive),
                _ => query
            };

            var pagedItems = await PaginatedList<InventoryItem>.CreateAsync(
                query.OrderBy(i => i.ItemName),
                page,
                pageSize);

            if (pagedItems.TotalPages > 0 && page > pagedItems.TotalPages)
            {
                page = pagedItems.TotalPages;

                pagedItems = await PaginatedList<InventoryItem>.CreateAsync(
                    query.OrderBy(i => i.ItemName),
                    page,
                    pageSize);
            }

            var itemIds = pagedItems.Select(i => i.ID).ToList();

            var locationCounts = new Dictionary<int, int>();
            var visibleQuantities = new Dictionary<int, int>();
            var locationQuantities = new Dictionary<int, List<LocationStockVM>>();

            if (itemIds.Count > 0)
            {
                IQueryable<InventoryItemStock> stockQuery = _context.InventoryItemStocks
                    .AsNoTracking()
                    .Include(s => s.StorageLocation)
                    .Where(s => itemIds.Contains(s.InventoryItemID));

                if (scope.IsScopedUser)
                {
                    var locationId = scope.AssignedLocationId!.Value;
                    stockQuery = stockQuery.Where(s => s.StorageLocationID == locationId);
                }

                var stockRows = await stockQuery
                    .OrderBy(s => s.StorageLocation!.Name)
                    .ToListAsync();

                locationQuantities = stockRows
                    .GroupBy(s => s.InventoryItemID)
                    .ToDictionary(
                        g => g.Key,
                        g => g
                            .OrderByDescending(x => x.QuantityOnHand)
                            .ThenBy(x => x.StorageLocation!.Name)
                            .Select(x => new LocationStockVM
                            {
                                LocationID = x.StorageLocationID,
                                LocationName = x.StorageLocation?.Name ?? "-",
                                QuantityOnHand = x.QuantityOnHand
                            })
                            .ToList());

                foreach (var kv in locationQuantities)
                {
                    locationCounts[kv.Key] = kv.Value.Count(x => x.QuantityOnHand > 0);
                    visibleQuantities[kv.Key] = kv.Value.Sum(x => x.QuantityOnHand);
                }
            }

            foreach (var item in pagedItems)
            {
                if (!locationQuantities.ContainsKey(item.ID))
                {
                    if (scope.HasGlobalLocationAccess)
                    {
                        locationQuantities[item.ID] = new List<LocationStockVM>
                        {
                            new LocationStockVM
                            {
                                LocationID = item.StorageLocationID,
                                LocationName = item.StorageLocation?.Name ?? "-",
                                QuantityOnHand = item.QuantityOnHand
                            }
                        };

                        visibleQuantities[item.ID] = item.QuantityOnHand;
                        locationCounts[item.ID] = item.QuantityOnHand > 0 ? 1 : 0;
                    }
                    else
                    {
                        var isPrimaryAssignedLocation = item.StorageLocationID == scope.AssignedLocationId;

                        locationQuantities[item.ID] = new List<LocationStockVM>
                        {
                            new LocationStockVM
                            {
                                LocationID = scope.AssignedLocationId ?? item.StorageLocationID,
                                LocationName = scope.AssignedLocationName ?? "-",
                                QuantityOnHand = isPrimaryAssignedLocation ? item.QuantityOnHand : 0
                            }
                        };

                        visibleQuantities[item.ID] = isPrimaryAssignedLocation ? item.QuantityOnHand : 0;
                        locationCounts[item.ID] = visibleQuantities[item.ID] > 0 ? 1 : 0;
                    }
                }

                if (scope.IsScopedUser &&
                    !string.IsNullOrWhiteSpace(scope.AssignedLocationName))
                {
                    item.StorageLocation = new StorageLocation
                    {
                        ID = scope.AssignedLocationId ?? item.StorageLocationID,
                        Name = scope.AssignedLocationName
                    };
                }
            }

            ViewData["LocationCounts"] = locationCounts;
            ViewData["VisibleQuantities"] = visibleQuantities;
            ViewData["LocationQuantities"] = locationQuantities;
            ViewData["HasGlobalLocationAccess"] = scope.HasGlobalLocationAccess;
            ViewData["AssignedLocationName"] = scope.AssignedLocationName ?? "-";
            ViewData["CurrentFilter"] = searchString ?? string.Empty;
            ViewData["CurrentStatus"] = statusFilter;
            ViewData["CurrentPageSize"] = pageSize;

            return View(pagedItems);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var scope = await _accessService.GetScopeAsync(User);

            IQueryable<InventoryItem> itemQuery = _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.ItemPhoto)
                .Include(i => i.ItemThumbnail)
                .Include(i => i.Category)
                .Include(i => i.StorageLocation);

            itemQuery = ApplyInventoryScope(itemQuery, scope);

            var item = await itemQuery.FirstOrDefaultAsync(m => m.ID == id);
            if (item == null) return NotFound();

            var stocksQuery = _context.InventoryItemStocks
                .AsNoTracking()
                .Include(s => s.StorageLocation)
                .Where(s => s.InventoryItemID == item.ID)
                .OrderByDescending(s => s.QuantityOnHand)
                .AsQueryable();

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                stocksQuery = stocksQuery.Where(s => s.StorageLocationID == locationId);
            }

            var stocks = await stocksQuery.ToListAsync();

            if (stocks.Count == 0)
            {
                if (scope.HasGlobalLocationAccess || item.StorageLocationID == scope.AssignedLocationId)
                {
                    stocks = new List<InventoryItemStock>
                    {
                        new InventoryItemStock
                        {
                            InventoryItemID = item.ID,
                            StorageLocationID = scope.IsScopedUser
                                ? scope.AssignedLocationId!.Value
                                : item.StorageLocationID,
                            StorageLocation = scope.IsScopedUser
                                ? new StorageLocation
                                {
                                    ID = scope.AssignedLocationId!.Value,
                                    Name = scope.AssignedLocationName ?? "-"
                                }
                                : (item.StorageLocation ?? new StorageLocation { Name = "-" }),
                            QuantityOnHand = item.QuantityOnHand
                        }
                    };
                }
            }

            var txQuery = _context.StockTransactions
                .AsNoTracking()
                .Include(t => t.FromStorageLocation)
                .Include(t => t.ToStorageLocation)
                .Where(t => t.InventoryItemID == item.ID)
                .OrderByDescending(t => t.DateCreated)
                .AsQueryable();

            if (scope.IsScopedUser)
            {
                var locationId = scope.AssignedLocationId!.Value;
                txQuery = txQuery.Where(t =>
                    t.FromStorageLocationID == locationId ||
                    t.ToStorageLocationID == locationId);
            }

            item.StockTransactions = await txQuery.ToListAsync();

            if (scope.IsScopedUser &&
                item.StorageLocationID != scope.AssignedLocationId &&
                !string.IsNullOrWhiteSpace(scope.AssignedLocationName))
            {
                item.StorageLocation = new StorageLocation
                {
                    ID = scope.AssignedLocationId!.Value,
                    Name = scope.AssignedLocationName
                };
            }

            ViewData["LocationStocks"] = stocks;
            ViewData["LocationCount"] = stocks.Count(s => s.QuantityOnHand > 0);
            ViewData["AiPrediction"] = await _inventoryAiService.GetPredictionAsync(item.ID, scope);
            ViewData["ReorderVm"] = await _orderService.BuildCreateVmAsync(item.ID, User);

            return View(item);
        }


        [HttpGet]
        public async Task<IActionResult> LookupByBarcode(string barcode, string? returnTo = null)
        {
            var normalized = BarcodeService.NormalizeBarcode(barcode);
            if (string.IsNullOrWhiteSpace(normalized))
                return RedirectToAction(nameof(Index));

            var scope = await _accessService.GetScopeAsync(User);
            var query = ApplyInventoryScope(_context.InventoryItems.AsNoTracking(), scope);
            var item = await query.FirstOrDefaultAsync(i => i.Barcode == normalized);
            if (item == null)
            {
                TempData["ErrorMessage"] = $"Barcode '{normalized}' was not found.";
                return Redirect(returnTo ?? Url.Action(nameof(Index))!);
            }

            if (!string.IsNullOrWhiteSpace(returnTo) && returnTo.Contains("StockTransactions", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Create", "StockTransactions", new { inventoryItemId = item.ID });

            if (!string.IsNullOrWhiteSpace(returnTo) && returnTo.Contains("StockTransferRequests", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Create", "StockTransferRequests", new { inventoryItemId = item.ID });

            return RedirectToAction(nameof(Details), new { id = item.ID });
        }

        [HttpGet]
        public async Task<IActionResult> ResolveByBarcode(string barcode)
        {
            var normalized = BarcodeService.NormalizeBarcode(barcode);
            var scope = await _accessService.GetScopeAsync(User);
            var item = await ApplyInventoryScope(_context.InventoryItems.AsNoTracking(), scope)
                .Select(i => new { i.ID, i.ItemName, i.SKU, i.Barcode, i.QuantityOnHand })
                .FirstOrDefaultAsync(i => i.Barcode == normalized);

            if (item == null)
                return NotFound(new { message = "Barcode not found." });

            return Json(item);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> BarcodeImage(int id)
        {
            var barcode = await _context.InventoryItems
                .AsNoTracking()
                .Where(i => i.ID == id)
                .Select(i => i.Barcode)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(barcode))
                return NotFound();

            var bytes = _barcodeService.RenderCode39Png(barcode);
            return File(bytes, "image/png");
        }

        [HttpGet]
        public IActionResult Scan() => View();

        [Authorize(Roles =
            AppRoles.Admin + "," +
            AppRoles.RegionalManager + "," +
            AppRoles.Manager + "," +
            AppRoles.Supervisor)]
        public async Task<IActionResult> Create()
        {
            var scope = await _accessService.GetScopeAsync(User);
            await PopulateDropDownsAsync(scope);
            return View();
        }

        [Authorize(Roles =
            AppRoles.Admin + "," +
            AppRoles.RegionalManager + "," +
            AppRoles.Manager + "," +
            AppRoles.Supervisor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ID,ItemName,SKU,Description,QuantityOnHand,ReorderLevel,IsActive,CategoryID,StorageLocationID")] InventoryItem item,
            IFormFile? thePicture)
        {
            var scope = await _accessService.GetScopeAsync(User);

            item.SKU = NormalizeSku(item.SKU);

            if (scope.IsScopedUser)
            {
                item.StorageLocationID = scope.AssignedLocationId!.Value;
            }

            if (await SkuExistsAsync(item.SKU))
                ModelState.AddModelError(nameof(InventoryItem.SKU), "SKU / Asset Tag must be unique. This value already exists.");

            if (string.IsNullOrWhiteSpace(item.Barcode))
                item.Barcode = await _barcodeService.GenerateUniqueBarcodeAsync();

            if (ModelState.IsValid)
            {
                var imgError = await TryAddPictureAsync(item, thePicture);
                if (imgError != null)
                {
                    ModelState.AddModelError(string.Empty, imgError);
                    ModelState.AddModelError("thePicture", imgError);
                }
            }

            if (ModelState.IsValid)
            {
                var strategy = _context.Database.CreateExecutionStrategy();

                try
                {
                    await strategy.ExecuteAsync(async () =>
                    {
                        await using var tx = await _context.Database.BeginTransactionAsync();

                        _context.InventoryItems.Add(item);
                        await _context.SaveChangesAsync();

                        _context.InventoryItemStocks.Add(new InventoryItemStock
                        {
                            InventoryItemID = item.ID,
                            StorageLocationID = item.StorageLocationID,
                            QuantityOnHand = item.QuantityOnHand
                        });

                        await _context.SaveChangesAsync();
                        await tx.CommitAsync();
                    });

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Unable to save changes. Please try again (SKU must be unique).");
                }
            }

            await PopulateDropDownsAsync(scope, item.CategoryID, item.StorageLocationID);
            return View(item);
        }

        [Authorize(Roles =
            AppRoles.Admin + "," +
            AppRoles.RegionalManager + "," +
            AppRoles.Manager + "," +
            AppRoles.Supervisor)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var scope = await _accessService.GetScopeAsync(User);

            var item = await _context.InventoryItems
                .Include(p => p.ItemPhoto)
                .Include(p => p.ItemThumbnail)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            if (scope.IsScopedUser && item.StorageLocationID != scope.AssignedLocationId)
                return Forbid();

            await PopulateDropDownsAsync(scope, item.CategoryID, item.StorageLocationID);
            return View(item);
        }

        [Authorize(Roles =
            AppRoles.Admin + "," +
            AppRoles.RegionalManager + "," +
            AppRoles.Manager + "," +
            AppRoles.Supervisor)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("ID,ItemName,SKU,Description,QuantityOnHand,ReorderLevel,IsActive,CategoryID,StorageLocationID")] InventoryItem item,
            string chkRemoveImage,
            IFormFile? thePicture)
        {
            if (id != item.ID) return NotFound();

            var scope = await _accessService.GetScopeAsync(User);

            var itemToUpdate = await _context.InventoryItems
                .Include(p => p.ItemPhoto)
                .Include(p => p.ItemThumbnail)
                .FirstOrDefaultAsync(i => i.ID == id);

            if (itemToUpdate == null) return NotFound();

            if (scope.IsScopedUser && itemToUpdate.StorageLocationID != scope.AssignedLocationId)
                return Forbid();

            item.SKU = NormalizeSku(item.SKU);

            if (await SkuExistsAsync(item.SKU, excludeId: item.ID))
                ModelState.AddModelError(nameof(InventoryItem.SKU), "SKU / Asset Tag must be unique. This value already exists.");

            if (item.QuantityOnHand != itemToUpdate.QuantityOnHand)
            {
                ModelState.AddModelError(nameof(InventoryItem.QuantityOnHand),
                    "Use Stock Transactions to change quantities (supports per-location stock and partial transfers).");
            }

            if (item.StorageLocationID != itemToUpdate.StorageLocationID)
            {
                ModelState.AddModelError(nameof(InventoryItem.StorageLocationID),
                    "Use Stock Transactions or Transfer Requests to move quantities between locations.");
            }

            itemToUpdate.ItemName = item.ItemName;
            itemToUpdate.SKU = item.SKU;
            itemToUpdate.Description = item.Description;
            itemToUpdate.ReorderLevel = item.ReorderLevel;
            itemToUpdate.IsActive = item.IsActive;
            itemToUpdate.CategoryID = item.CategoryID;

            if (!string.IsNullOrEmpty(chkRemoveImage) && chkRemoveImage == "on")
            {
                if (itemToUpdate.ItemPhoto != null)
                {
                    _context.ItemPhotos.Remove(itemToUpdate.ItemPhoto);
                    itemToUpdate.ItemPhoto = null;
                }

                if (itemToUpdate.ItemThumbnail != null)
                {
                    _context.ItemThumbnails.Remove(itemToUpdate.ItemThumbnail);
                    itemToUpdate.ItemThumbnail = null;
                }
            }

            if (ModelState.IsValid)
            {
                var imgError = await TryAddPictureAsync(itemToUpdate, thePicture);
                if (imgError != null)
                {
                    ModelState.AddModelError(string.Empty, imgError);
                    ModelState.AddModelError("thePicture", imgError);
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Unable to save changes. Please try again.");
                }
            }

            await PopulateDropDownsAsync(scope, item.CategoryID, itemToUpdate.StorageLocationID);
            return View(itemToUpdate);
        }

        [Authorize(Roles =
            AppRoles.Admin + "," +
            AppRoles.RegionalManager + "," +
            AppRoles.Manager + "," +
            AppRoles.Supervisor)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var scope = await _accessService.GetScopeAsync(User);

            var item = await _context.InventoryItems
                .Include(i => i.Category)
                .Include(i => i.StorageLocation)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            if (scope.IsScopedUser && item.StorageLocationID != scope.AssignedLocationId)
                return Forbid();

            return View(item);
        }

        [Authorize(Roles =
            AppRoles.Admin + "," +
            AppRoles.RegionalManager + "," +
            AppRoles.Manager + "," +
            AppRoles.Supervisor)]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var scope = await _accessService.GetScopeAsync(User);

            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return RedirectToAction(nameof(Index));

            if (scope.IsScopedUser && item.StorageLocationID != scope.AssignedLocationId)
                return Forbid();

            try
            {
                _context.InventoryItems.Remove(item);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty, "Unable to delete this item because it has related records (transactions or stock rows).");
                return View("Delete", item);
            }
        }
    }
}