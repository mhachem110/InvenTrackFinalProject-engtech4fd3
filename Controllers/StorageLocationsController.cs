using InvenTrack.Data;
using InvenTrack.Models;
using InvenTrack.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InvenTrack.Controllers
{
    [Authorize(Roles = "Admin,Manager,Viewer")]
    public class StorageLocationsController : Controller
    {
        private readonly InvenTrackContext _context;

        public StorageLocationsController(InvenTrackContext context)
        {
            _context = context;
        }

        // ---------------------------
        // Helpers
        // ---------------------------

        private static string Clean(string? s) => (s ?? string.Empty).Trim();

        private async Task<bool> LocationNameExistsAsync(string name, int? excludeId = null)
        {
            name = Clean(name);
            if (string.IsNullOrWhiteSpace(name)) return false;

            return await _context.StorageLocations.AnyAsync(l =>
                l.Name == name && (!excludeId.HasValue || l.ID != excludeId.Value));
        }

        // GET: StorageLocations
        public async Task<IActionResult> Index(string? searchString, int page = 1, int pageSize = 10)
        {
            if (page < 1) page = 1;

            var allowedPageSizes = new[] { 10, 25, 50 };
            if (!allowedPageSizes.Contains(pageSize))
                pageSize = 10;

            IQueryable<StorageLocation> query = _context.StorageLocations
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();

                query = query.Where(l =>
                    l.Name.Contains(searchString) ||
                    (l.Building != null && l.Building.Contains(searchString)) ||
                    (l.Room != null && l.Room.Contains(searchString)));
            }

            var pagedLocations = await PaginatedList<StorageLocation>.CreateAsync(
                query.OrderBy(l => l.Name),
                page,
                pageSize);

            if (pagedLocations.TotalPages > 0 && page > pagedLocations.TotalPages)
            {
                page = pagedLocations.TotalPages;

                pagedLocations = await PaginatedList<StorageLocation>.CreateAsync(
                    query.OrderBy(l => l.Name),
                    page,
                    pageSize);
            }

            ViewData["CurrentFilter"] = searchString ?? string.Empty;
            ViewData["CurrentPageSize"] = pageSize;

            return View(pagedLocations);
        }

        // GET: StorageLocations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var storageLocation = await _context.StorageLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (storageLocation == null) return NotFound();

            return View(storageLocation);
        }

        // GET: StorageLocations/Create
        [Authorize(Roles = "Admin,Manager")]

        public IActionResult Create()
        {
            return View();
        }

        [Authorize(Roles = "Admin,Manager")]
        // POST: StorageLocations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Building,Room")] StorageLocation storageLocation)
        {
            storageLocation.Name = Clean(storageLocation.Name);
            storageLocation.Building = Clean(storageLocation.Building);
            storageLocation.Room = Clean(storageLocation.Room);

            if (await LocationNameExistsAsync(storageLocation.Name))
            {
                ModelState.AddModelError(nameof(StorageLocation.Name), "Location name must be unique. This location already exists.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(storageLocation);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Unable to save location. Please try again.");
                }
            }

            return View(storageLocation);
        }

        // GET: StorageLocations/Edit/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var storageLocation = await _context.StorageLocations.FindAsync(id);
            if (storageLocation == null) return NotFound();

            return View(storageLocation);
        }

        // POST: StorageLocations/Edit/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id)
        {
            var storageLocationToUpdate = await _context.StorageLocations.FirstOrDefaultAsync(l => l.ID == id);
            if (storageLocationToUpdate == null) return NotFound();

            if (await TryUpdateModelAsync(storageLocationToUpdate, "",
                l => l.Name, l => l.Building, l => l.Room))
            {
                storageLocationToUpdate.Name = Clean(storageLocationToUpdate.Name);
                storageLocationToUpdate.Building = Clean(storageLocationToUpdate.Building);
                storageLocationToUpdate.Room = Clean(storageLocationToUpdate.Room);

                if (await LocationNameExistsAsync(storageLocationToUpdate.Name, excludeId: id))
                {
                    ModelState.AddModelError(nameof(StorageLocation.Name), "Location name must be unique. This location already exists.");
                    return View(storageLocationToUpdate);
                }

                try
                {
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.StorageLocations.AnyAsync(e => e.ID == id))
                        return NotFound();

                    throw;
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError(string.Empty, "Unable to save changes. Please try again.");
                }
            }

            return View(storageLocationToUpdate);
        }

        // GET: StorageLocations/Delete/5
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var storageLocation = await _context.StorageLocations
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ID == id);

            if (storageLocation == null) return NotFound();

            return View(storageLocation);
        }

        // POST: StorageLocations/Delete/5
        [Authorize(Roles = "Admin,Manager")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var storageLocation = await _context.StorageLocations.FindAsync(id);
            if (storageLocation == null) return RedirectToAction(nameof(Index));

            try
            {
                _context.StorageLocations.Remove(storageLocation);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateException)
            {
                ModelState.AddModelError(string.Empty,
                    "Unable to delete this location because it is being used by one or more inventory items.");

                return View("Delete", storageLocation);
            }
        }
    }
}