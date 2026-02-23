using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InvenTrack.Data;
using InvenTrack.Models;

namespace InvenTrack.Controllers
{
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
        public async Task<IActionResult> Index()
        {
            var locations = await _context.StorageLocations
                .AsNoTracking()
                .OrderBy(l => l.Name)
                .ToListAsync();

            return View(locations);
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
        public IActionResult Create()
        {
            return View();
        }

        // POST: StorageLocations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Building,Room")] StorageLocation storageLocation)
        {
            // Normalize text inputs
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
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var storageLocation = await _context.StorageLocations.FindAsync(id);
            if (storageLocation == null) return NotFound();

            return View(storageLocation);
        }

        // POST: StorageLocations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id)
        {
            var storageLocationToUpdate = await _context.StorageLocations.FirstOrDefaultAsync(l => l.ID == id);
            if (storageLocationToUpdate == null) return NotFound();

            // Update only allowed fields
            if (await TryUpdateModelAsync(storageLocationToUpdate, "",
                l => l.Name, l => l.Building, l => l.Room))
            {
                // Normalize
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