using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin,Manager,Viewer")]
    public class InventoryItemsController : Controller
    {
        private readonly InvenTrackContext _context;

        public InventoryItemsController(InvenTrackContext context)
        {
            _context = context;
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

        private void PopulateDropDowns(int? categoryID = null, int? locationID = null)
        {
            ViewData["CategoryID"] = new SelectList(_context.Categories.OrderBy(c => c.Name), "ID", "Name", categoryID);
            ViewData["StorageLocationID"] = new SelectList(_context.StorageLocations.OrderBy(l => l.Name), "ID", "Name", locationID);
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

        public async Task<IActionResult> Index()
        {
            var items = await _context.InventoryItems
                .AsNoTracking()
                .Include(i => i.ItemThumbnail)
                .Include(i => i.Category)
                .Include(i => i.StorageLocation)
                .OrderBy(i => i.ItemName)
                .ToListAsync();

            var counts = await _context.InventoryItemStocks
                .AsNoTracking()
                .GroupBy(s => s.InventoryItemID)
                .Select(g => new
                {
                    InventoryItemID = g.Key,
                    LocationCount = g.Count(x => x.QuantityOnHand > 0)
                })
                .ToListAsync();

            var dict = counts.ToDictionary(x => x.InventoryItemID, x => x.LocationCount);
            ViewData["LocationCounts"] = dict;

            return View(items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.InventoryItems
                .Include(i => i.ItemPhoto)
                .Include(i => i.ItemThumbnail)
                .Include(i => i.Category)
                .Include(i => i.StorageLocation)
                .Include(i => i.StockTransactions).ThenInclude(t => t.FromStorageLocation)
                .Include(i => i.StockTransactions).ThenInclude(t => t.ToStorageLocation)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            var stocks = await _context.InventoryItemStocks
                .AsNoTracking()
                .Include(s => s.StorageLocation)
                .Where(s => s.InventoryItemID == item.ID)
                .OrderByDescending(s => s.QuantityOnHand)
                .ToListAsync();

            if (stocks.Count == 0)
            {
                stocks = new List<InventoryItemStock>
                {
                    new InventoryItemStock
                    {
                        InventoryItemID = item.ID,
                        StorageLocationID = item.StorageLocationID,
                        StorageLocation = item.StorageLocation ?? new StorageLocation { Name = "-" },
                        QuantityOnHand = item.QuantityOnHand
                    }
                };
            }

            ViewData["LocationStocks"] = stocks;
            ViewData["LocationCount"] = stocks.Count(s => s.QuantityOnHand > 0);

            return View(item);
        }

        public IActionResult Create()
        {
            PopulateDropDowns();
            return View();
        }

        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            [Bind("ID,ItemName,SKU,Description,QuantityOnHand,ReorderLevel,IsActive,CategoryID,StorageLocationID")] InventoryItem item,
            IFormFile? thePicture)
        {
            item.SKU = NormalizeSku(item.SKU);

            if (await SkuExistsAsync(item.SKU))
                ModelState.AddModelError(nameof(InventoryItem.SKU), "SKU / Asset Tag must be unique. This value already exists.");

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

                        _context.Add(item);
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

            PopulateDropDowns(item.CategoryID, item.StorageLocationID);
            return View(item);
        }
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.InventoryItems
                .Include(p => p.ItemPhoto)
                .Include(p => p.ItemThumbnail)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            PopulateDropDowns(item.CategoryID, item.StorageLocationID);
            return View(item);
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("ID,ItemName,SKU,Description,QuantityOnHand,ReorderLevel,IsActive,CategoryID,StorageLocationID")] InventoryItem item,
            string chkRemoveImage,
            IFormFile? thePicture)
        {
            if (id != item.ID) return NotFound();

            var itemToUpdate = await _context.InventoryItems
                .Include(p => p.ItemPhoto)
                .Include(p => p.ItemThumbnail)
                .FirstOrDefaultAsync(i => i.ID == id);

            if (itemToUpdate == null) return NotFound();

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
                    "Use Stock Transactions (Transfer) to move quantities between locations.");
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

            PopulateDropDowns(item.CategoryID, itemToUpdate.StorageLocationID);
            return View(itemToUpdate);
        }
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.InventoryItems
                .Include(i => i.Category)
                .Include(i => i.StorageLocation)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            return View(item);
        }
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item == null) return RedirectToAction(nameof(Index));

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