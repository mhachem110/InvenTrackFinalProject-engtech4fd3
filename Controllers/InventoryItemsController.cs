using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Utilities;

namespace InvenTrack.Controllers
{
    public class InventoryItemsController : Controller
    {
        private readonly InvenTrackContext _context;

        public InventoryItemsController(InvenTrackContext context)
        {
            _context = context;
        }

        // GET: InventoryItems
        public async Task<IActionResult> Index()
        {
            var items = _context.InventoryItems
                .Include(i => i.ItemThumbnail)
                .Include(i => i.Category)
                .Include(i => i.StorageLocation);

            return View(await items.ToListAsync());
        }

        // GET: InventoryItems/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.InventoryItems
                .Include(i => i.ItemPhoto)
                .Include(i => i.Category)
                .Include(i => i.StorageLocation)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            return View(item);
        }

        // GET: InventoryItems/Create
        public IActionResult Create()
        {
            PopulateDropDowns();
            return View();
        }

        // POST: InventoryItems/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ID,ItemName,SKU,Description,QuantityOnHand,ReorderLevel,IsActive,CategoryID,StorageLocationID")] InventoryItem item,
                                               IFormFile thePicture)
        {
            if (ModelState.IsValid)
            {
                await AddPicture(item, thePicture);
                _context.Add(item);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            PopulateDropDowns(item.CategoryID, item.StorageLocationID);
            return View(item);
        }

        // GET: InventoryItems/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var item = await _context.InventoryItems
                .Include(p => p.ItemPhoto)
                .FirstOrDefaultAsync(m => m.ID == id);

            if (item == null) return NotFound();

            PopulateDropDowns(item.CategoryID, item.StorageLocationID);
            return View(item);
        }

        // POST: InventoryItems/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id,
            [Bind("ID,ItemName,SKU,Description,QuantityOnHand,ReorderLevel,IsActive,CategoryID,StorageLocationID")] InventoryItem item,
            string chkRemoveImage,
            IFormFile thePicture)
        {
            if (id != item.ID) return NotFound();

            var itemToUpdate = await _context.InventoryItems
                .Include(p => p.ItemPhoto)
                .Include(p => p.ItemThumbnail)
                .FirstOrDefaultAsync(i => i.ID == id);

            if (itemToUpdate == null) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Update scalar fields
                    itemToUpdate.ItemName = item.ItemName;
                    itemToUpdate.SKU = item.SKU;
                    itemToUpdate.Description = item.Description;
                    itemToUpdate.QuantityOnHand = item.QuantityOnHand;
                    itemToUpdate.ReorderLevel = item.ReorderLevel;
                    itemToUpdate.IsActive = item.IsActive;
                    itemToUpdate.CategoryID = item.CategoryID;
                    itemToUpdate.StorageLocationID = item.StorageLocationID;

                    // Remove image if requested
                    if (!string.IsNullOrEmpty(chkRemoveImage) && chkRemoveImage == "on")
                    {
                        if (itemToUpdate.ItemPhoto != null)
                        {
                            _context.ItemPhotos.Remove(itemToUpdate.ItemPhoto);
                        }
                        if (itemToUpdate.ItemThumbnail != null)
                        {
                            _context.ItemThumbnails.Remove(itemToUpdate.ItemThumbnail);
                        }
                    }

                    await AddPicture(itemToUpdate, thePicture);

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // likely a unique index conflict (SKU/ReferenceNumber)
                    ModelState.AddModelError("", "Unable to save changes. Make sure the SKU is unique.");
                    PopulateDropDowns(item.CategoryID, item.StorageLocationID);
                    return View(itemToUpdate);
                }
                return RedirectToAction(nameof(Index));
            }

            PopulateDropDowns(item.CategoryID, item.StorageLocationID);
            return View(itemToUpdate);
        }

        // GET: InventoryItems/Delete/5
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

        // POST: InventoryItems/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var item = await _context.InventoryItems.FindAsync(id);
            if (item != null)
            {
                _context.InventoryItems.Remove(item);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private void PopulateDropDowns(int? categoryID = null, int? locationID = null)
        {
            ViewData["CategoryID"] = new SelectList(_context.Categories.OrderBy(c => c.Name), "ID", "Name", categoryID);
            ViewData["StorageLocationID"] = new SelectList(_context.StorageLocations.OrderBy(l => l.Name), "ID", "Name", locationID);
        }

        private async Task AddPicture(InventoryItem item, IFormFile thePicture)
        {
            // Reuse existing image logic: store a full WebP + a thumbnail WebP
            if (thePicture != null)
            {
                string mimeType = thePicture.ContentType;
                long fileLength = thePicture.Length;

                if (!(mimeType == "" || fileLength == 0) && mimeType.Contains("image"))
                {
                    using var memoryStream = new MemoryStream();
                    await thePicture.CopyToAsync(memoryStream);
                    var pictureArray = memoryStream.ToArray();

                    if (item.ItemPhoto != null)
                    {
                        item.ItemPhoto.Content = ResizeImage.shrinkImageWebp(pictureArray, 500, 600);

                        // ensure thumbnail loaded/exists
                        item.ItemThumbnail = _context.ItemThumbnails
                            .Where(p => p.InventoryItemID == item.ID)
                            .FirstOrDefault() ?? item.ItemThumbnail;

                        if (item.ItemThumbnail == null)
                        {
                            item.ItemThumbnail = new ItemThumbnail { MimeType = "image/webp" };
                        }
                        item.ItemThumbnail.Content = ResizeImage.shrinkImageWebp(pictureArray, 100, 120);
                    }
                    else
                    {
                        item.ItemPhoto = new ItemPhoto
                        {
                            Content = ResizeImage.shrinkImageWebp(pictureArray, 500, 600),
                            MimeType = "image/webp"
                        };
                        item.ItemThumbnail = new ItemThumbnail
                        {
                            Content = ResizeImage.shrinkImageWebp(pictureArray, 100, 120),
                            MimeType = "image/webp"
                        };
                    }
                }
            }
        }
    }
}
